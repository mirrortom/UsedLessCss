using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace UsedLessCss;
internal class UsedCss
{
    #region 成员定义

    /// <summary>
    /// 简单css预定义规则集合,主要是工具样式,使用单个类选择器,规则数目主要是1条的,也有少量多条的 
    /// </summary>
    private static Dictionary<string, string> simpleRules;

    /// <summary>
    /// 预定义的css规则名字,在动态生成规则时,约定的简化class名字和实际名字映射集合
    /// 例如: "mg-l",对应"margin-left"
    /// </summary>
    private static Dictionary<string, string> styleNamesSimple;

    /// <summary>
    /// 预定义css值,比如颜色(格式#000aaa),字体粗细100-900等等
    /// 键是样式类型和值的组合,例如 "red5:#ef4444",red表示颜色,5表示颜色值. "b7:700",b表示blod,7表示700.
    /// </summary>
    private static Dictionary<string, string> styleValues;

    /// <summary>
    /// 特定css规则处理集合,返回样式的值.键是简化类名,值是处理方法
    /// 0:一般性处理,返回像素px或者百分比%单位的值.
    /// 其它特别处理方法在此加入
    /// </summary>
    private static Dictionary<string, Func<string, string>> rulesProcMethod = new()
    {
        // 用于grid布局,平均分配列宽
        ["grid-cols"] = (val) =>
        {
            if ((int.TryParse(val, out int _)))
            {
                return val;
            }
            return string.Empty;
        },
        // 用于grid布局,百分比分配列宽 grid-[num]p[num]p
        // 例如: "grid-10p"=>grid-template-columns:10% 90%
        // "grid-10p30p60p"=>grid-template-columns:10% 30% 60%
        ["grid"] = (val) =>
        {
            if (!val.EndsWith('p'))
                return string.Empty;
            string[] percents = val[..^1].Split('p');
            int[] vals = new int[percents.Length];
            for (int i = 0; i < vals.Length; i++)
            {
                if (!int.TryParse(percents[i], out vals[i]))
                    return string.Empty;
            }
            // 如果值的和已经超过100,直接返回,否则加上最后列.
            int max100 = 0;
            StringBuilder reVal = new();
            foreach (var item in vals)
            {
                max100 += item;
                reVal.Append($"{item}% ");
            }
            if (max100 >= 100)
                return reVal.ToString();
            return reVal.Append($"{100 - max100}%").ToString();
        },
        // 通用规则 键名特别取值,不使用class类名.class名字不能数字开头,不能含有%,#等特殊符号
        ["0"] = (val) =>
        {
            if ((int.TryParse(val, out int _)))
            {
                // 纯数值默认是像素单位
                return $"{val}px";
            }
            string[] unit = ["rem", "em", "vw", "vh", "px"];
            foreach (var item in unit)
            {
                // 自带了css单位,并且数值有效,直接使用
                if (val.EndsWith(item, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(val.Replace(item, ""), out int _val))
                {
                    return val;
                }
            }
            // p结尾是百分比单位,单独处理
            if (val.EndsWith("p", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(val[..^1], out int _valp))
            {
                return _valp + "%";
            }
            return string.Empty;
        }
    };

    /// <summary>
    /// 匹配成功的css样式的缓存器,键是选择器,值是规则集合,HashSet每个元素是一个规则
    /// 使用这个特点的集合可以去掉重复的选择器和重复的规则
    /// </summary>
    private Dictionary<string, HashSet<string>> buffer;

    /// <summary>
    /// 一次处理时,忽略样式名字列表
    /// </summary>
    private HashSet<string> ignoreClassList;

    /// <summary>
    /// 一次处理时,明确要加入的样式名字列表
    /// </summary>
    private HashSet<string> explicitlyClassList;

    /// <summary>
    /// 一次处理时,存放提取的样式类名.注意实例化时用忽略大小写的参数
    /// </summary>
    private HashSet<string> classSet;

    /// <summary>
    /// 一次处理的html文件列表.这个成员和classFileIdMap,记录类名来自哪些文件,用于错误提示.
    /// </summary>
    private string[] inputFiles;

    /// <summary>
    /// 提取的样式类名字和来自提取的html文件的关系.例如btn=>[1,3],btn样式来自于文件1和3号.文件编号id是成员inputFiles的索引.字典的键和成员classSet相同.
    /// </summary>
    private Dictionary<string, HashSet<int>> classFileIdMap;

#if DEBUG
    private StringBuilder logBuf = new();
#endif
    #endregion

    /// <summary>
    /// 初始化对象时载入了数据,请调用Run方法完成任务.
    /// </summary>
    public UsedCss()
    {
        // 初始化规则集 使用静态成员,避免多次初始化
        simpleRules ??= RulesSimpleLoad("dataDefault/simpleRules.ini");
        // 载入简化类名和实际类名字典
        styleNamesSimple ??= RulesSimpleLoad("dataDefault/styleSimpleName.ini");
        // 载入css预定义值
        styleValues ??= RulesSimpleLoad("dataDefault/styleValues.ini");
    }

    /// <summary>
    /// htmlContent:html文件内容
    /// </summary>
    /// <param name="paths"></param>
    /// <returns></returns>
    public string Run(params string[] paths)
    {
        // 1.遍历html文件
        // 2.提取所有class名字
        ScanClassFromHtmlFiles(paths);

        // 3. 生成CSS
#if DEBUG
        Console.WriteLine("---CSS开始生成---");
        logBuf.AppendLine("---CSS开始生成---");
#endif
        GenerateCss(classSet);

        // 4.合并相同样式
        SameRulesCombine();

        // 5. 输出CSS文件
        var outCss = ToCssTxt();
#if DEBUG
        Console.WriteLine("CSS输出结束.");
        Console.WriteLine();
        Console.WriteLine("Log Log Log");
        Console.WriteLine(logBuf.ToString());
        Console.WriteLine("END END Log");
        string logFilePath = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.usedcss.log";
        Console.WriteLine($"A log file was created on {AppDomain.CurrentDomain.BaseDirectory}{logFilePath}");
        Console.WriteLine();
        // 文件log
        File.WriteAllText(logFilePath, logBuf.ToString());
#endif
        return outCss;
    }

    #region 提取html的class

    void ScanClassFromHtmlFiles(string[] paths)
    {
        // 忽略列表,如果么有,为空集
        this.ignoreClassList ??= [];
        this.inputFiles = paths;
        this.classFileIdMap = new(StringComparer.OrdinalIgnoreCase);
        // HashSet不会插入重复元素,避免提取相同的类名字
        classSet = new(StringComparer.OrdinalIgnoreCase);
        //
        for (int i = 0; i < inputFiles.Length; i++)
        {
            string itemPath = inputFiles[i];
            // 1. 读取HTML文件
            FileStream fStream = File.OpenRead(itemPath);
            // 2. 使用AngleSharp解析HTML并提取class
            var parser = new HtmlParser();
            var document = parser.ParseDocument(fStream);
            ExtractClasses(document, i);
        }
        // 添加明确加入的样式
        if (this.explicitlyClassList != null)
        {
            foreach (string item in this.explicitlyClassList)
            {
                classSet.Add(item);
            }
        }
    }

    /// <summary>
    /// 提取所有元素的class属性.返回提取的个数(已排除忽略的)
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    void ExtractClasses(AngleSharp.Dom.IDocument document, int fileIndex)
    {
        // 提取所有元素的class属性
        foreach (var element in document.All)
        {
            if (element.ClassList.Length <= 0)
                continue;
            // 类选择器列表
            foreach (var className in element.ClassList)
            {
                // 排除忽略样式
                if (ignoreClassList.FirstOrDefault(o => o.Equals
                (className, StringComparison.OrdinalIgnoreCase)) != null)
                    continue;
                // 提取到的类名
                string clsN = className.Trim();
                classSet.Add(clsN);
                // 记录类名和文件对应关系
                if (classFileIdMap.TryGetValue(clsN, out var classFileId))
                {
                    classFileId.Add(fileIndex);
                }
                else
                {
                    classFileIdMap[clsN] = [];
                    classFileIdMap[clsN].Add(fileIndex);
                }
            }
        }
    }

    /// <summary>
    /// 匹配样式,从各个源中抽取或者生成css
    /// </summary>
    /// <param name="classes"></param>
    void GenerateCss(HashSet<string> classes)
    {
        buffer = new(StringComparer.OrdinalIgnoreCase);
#if DEBUG
        Console.WriteLine($"总共{classes.Count}个样式需要匹配");

        Console.WriteLine();
        int successCount = 0;
        int codeIndex = 1;
#endif
        foreach (var clsName in classes.OrderBy(c => c))
        {
#if DEBUG
            Console.WriteLine($"开始匹配: {clsName}");
            logBuf.Append($"编号[{codeIndex}] 样式[{clsName}] 结果--");
#endif
            bool isok =
            // 简单预定义css匹配
            SimpleRuleMatch(clsName) ||
            // 简单动态值匹配
            DynamicRuleValueMatch(clsName);
            // 复杂预定义css匹配
            //ComplexRuleMatch(clsName);

#if DEBUG
            if (isok == false)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"匹配失败:( {clsName}");
                Console.ForegroundColor = ConsoleColor.White;
                // 匹配失败时,日志记录对应文件
                logBuf.Append($"匹配失败:( 受影响文件:");
                foreach (var fIndex in classFileIdMap[clsName])
                {
                    logBuf.Append($"[{Path.GetFileName(inputFiles[fIndex])}], ");
                }
                logBuf.AppendLine($"-----ERROR");
            }
            else
                successCount++;
            Console.WriteLine();
            codeIndex++;
#endif
        }
#if DEBUG
        logBuf.AppendLine($"总计匹配{classes.Count}个样式.成功{successCount}个.");
#endif
    }

    #endregion


    #region 载入规则集合,载入预定义数据

    /// <summary>
    /// css规则集载入
    /// 1.简单预定义css规则
    /// 2.简单预定义动态css简化类名对应规则
    /// 3.预定义css值
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static Dictionary<string, string> RulesSimpleLoad(string file)
    {
        // from rule file 
        var ruleLines = File.ReadAllLines(file);
        // load rule in dict
        Dictionary<string, string> rules = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ruleLines)
        {
            if (string.IsNullOrWhiteSpace(item) || item.StartsWith("//"))
                continue;
            var rule = item.Split('=');
            if (rule.Length != 2) continue;
            // 在cssSimpleName简化类名字源里,rule[0]可以是逗号分割的多个值.
            // 例如规则txt-red=color:red,txt-blue:color:blue的值都是color:xxx,就写在一起
            // txt-red,txt-blue,...=color:xxx
            string[] keys = rule[0].Split(',');
            foreach (var k in keys)
            {
                if (!string.IsNullOrWhiteSpace(k))
                {
                    // 键可能有重复的,因为配置条目在添加时可能用了相同的名字
                    if (rules.TryGetValue(k.Trim(), out _))
                        throw new Exception($"配置文件[{file}] 条目名字重复[{k}]!");
                    rules.Add(k.Trim(), rule[1].Trim());
                }
            }
        }
        //
        return rules;
    }

    /// <summary>
    /// 忽略样式列表
    /// </summary>
    /// <param name="clsNames"></param>
    public void IgnoreClassLoad(params string[] clsNames)
    {
        if (this.ignoreClassList != null) return;
        this.ignoreClassList = [.. clsNames];
    }

    /// <summary>
    /// 明确加入样式列表
    /// </summary>
    /// <param name="clsNames"></param>
    public void ExplicitlyClassListLoad(params string[] clsNames)
    {
        if (this.explicitlyClassList != null) return;
        this.explicitlyClassList = [.. clsNames];
    }
    #endregion


    #region 匹配,从几种规则集中取出规则

    /// <summary>
    /// 简单预定义css规则匹配,成功时返回true,规则加入输出缓存
    /// </summary>
    /// <param name="clsName"></param>
    /// <returns></returns>
    bool SimpleRuleMatch(string clsName)
    {
        if (simpleRules.TryGetValue(clsName, out var match))
        {
            AddBuffer(clsName, match, this.buffer);
#if DEBUG
            Console.WriteLine($"简单预定义匹配成功 {clsName}");
            logBuf.AppendLine($"简单预定义匹配-成功^_^!");
#endif
            return true;
        }
        return false;
    }

    /// <summary>
    /// 动态生成css规则,规则名字和值生成成功时,返回ture,规则加入输出缓存.
    /// 通用规则:pre[-x][-y]-val,第一个为前缀,最后一个为值
    /// 生成逻辑:根据样式类简化名查找规则名字,根据值查询或者生成样式值.
    /// 例如:"bg-red-7","bg-red"转为为规则名:"background-color","red-7"转为规则值:"#b91c1c"
    /// </summary>
    /// <param name="clsName"></param>
    /// <returns></returns>
    bool DynamicRuleValueMatch(string clsName)
    {
        var clsArr = clsName.Split('-');

        // 分析规则名字
        if (clsArr.Length < 2)
            return false;
        // 到字典找出对应类全名. match示例: mg => margin:$v
        string preName = string.Join('-', clsArr[..^1]);
        if (!styleNamesSimple.TryGetValue(preName, out var match))
            return false;

        // 分析规则值
        string ruleTypeName = clsArr[^2];
        string ruleValue = clsArr[^1];
        string targetVal = string.Empty;

        // 到字典找出预定义值
        if (styleValues.TryGetValue($"{ruleTypeName}{ruleValue}", out var matchValue))
        {
            targetVal = matchValue;
        }
        else
        {
            // 非预定义值时
            if (rulesProcMethod.TryGetValue(preName, out var method))
            {
                // 特定的规则处理
                targetVal = method(ruleValue);
            }
            else
            {
                // 一般性规则处理
                targetVal = rulesProcMethod["0"](ruleValue);
            }
        }
        // 值不符合要求
        if (targetVal == string.Empty)
            return false;

        // 替换为实际值,加入到输出缓存 match示例: margin:$v或者margin-left:$v;margin-right:$v
        string rules = match.Replace("$v", targetVal);
        AddBuffer(clsName, rules, this.buffer);
#if DEBUG
        Console.WriteLine($"简单动态匹配成功 {clsName}");
        logBuf.AppendLine($"简单动态匹配-成功^_^!");
#endif
        return true;
    }
    #endregion


    #region 匹配成功的规则加入缓存

    /// <summary>
    /// 将一个css样式加入到缓存
    /// </summary>
    /// <param name="name"></param>
    /// <param name="val"></param>
    /// <param name="buf"></param>
    void AddBuffer(string name, string val, Dictionary<string, HashSet<string>> buf)
    {
        // 相同的class名字,合并成一个样式
        if (!buf.TryGetValue(name, out var _))
        {
            buf[name] = [];
        }
        // hashset集合,避免加入相同规则
        var items = val.Split(';');
        foreach (var c in items)
        {
            // 拆解规则键值对,去掉空客,统一格式.避免相同规则重复加入.
            // 例如"left:0"和"left: 0"或" left:0"
            var r = c.Split(":");
            buf[name].Add($"{r[0].Trim()}:{r[1].Trim()}");
        }
    }
    #endregion

    #region 优化,格式化,生成输出css

    /// <summary>
    /// 生成css文本
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private string ToCssTxt()
    {
        StringBuilder sb = new();
        sb.AppendLine("/*-----usedCss--split--line-----*/");
        // 加入普通样式集
        foreach (var k in buffer.Keys)
        {
            FomartToTxtBuf(sb, k, buffer[k]);
        }
#if DEBUG
        sb.AppendLine($"/*-----usedCss-----Create time: {DateTime.Now.ToString()}-----*/");
#endif
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 将一个css样式和其下规则格式化写入输出缓存
    /// </summary>
    /// <param name="outBuf"></param>
    /// <param name="clsName"></param>
    /// <param name="rules"></param>
    /// <param name="indentCount"></param>
    private void FomartToTxtBuf(StringBuilder outBuf, string clsName, HashSet<string> rules, int indentCount = 2)
    {
        // 类名.(如果是写在媒体查询下的样式,需要缩进)
        string n = $".{clsName}";
        if (clsName.Contains(','))
        {
            // 如果类名是多个,比如txt,txt2,txt3
            n = string.Join(',', clsName.Split(",").Select(o => '.' + o));
        }
        outBuf.Append($"{"".PadLeft(indentCount - 2, ' ')}{n}");
        // 左括号,和类名之间有一个空格
        outBuf.AppendLine(" {");
        foreach (var c in rules)
        {
            // 规则输出格式统一为: [2空格]键和值冒号分割开,值左边有个空格,分号结束
            // 例如: "border-width: 1px;"
            var r = c.Split(':');
            // 类和规则之间,缩进单位2空格.
            outBuf.AppendLine($"{"".PadLeft(indentCount, ' ')}{r[0]}: {r[1]};");
        }
        // 右括号
        outBuf.AppendLine("".PadLeft(indentCount - 2, ' ') + "}");
        // 每个css类之间空一行
        outBuf.AppendLine();
    }

    /// <summary>
    /// 合并相同规则的样式.例如.a{color:red},.b{color:red}合并.a,.b{color:red}
    /// </summary>
    private void SameRulesCombine()
    {
        // 遍历缓存,查询含有完全相同规则的样式,合并成一个样式

        // 普通样式集 duplicates:相同的
        var dupGroupList = FindSameValueItems(this.buffer);
        if (dupGroupList.Count > 0)
        {
            // 遍历重复键集合
            foreach (var dupKeys in dupGroupList)
            {
                RemoveDupAndAddNew(dupKeys, this.buffer);
            }
        }

        // dupKeys:在src中具有相同值的键的列表.src:源字典
        // 该方法将src中所有相同值的元素为合并为一个,值不变,键是相同值的键的,以逗号合并的字符串
        void RemoveDupAndAddNew(List<string> dupKeys, Dictionary<string, HashSet<string>> src)
        {
            // 保留一份要删除的相同值元素
            var v = src[dupKeys[0]];
            // 从缓存这,删除这一组的相同值元素.
            foreach (var dk in dupKeys)
            {
                src.Remove(dk);
            }
            // 然后加入一个新元素,以相同值元素的值为值,键是逗号合并的所有相同元素的键
            src.Add(string.Join(',', dupKeys), v);
        }
    }

    /// <summary>
    /// 返回一个列表集合,元素是,源字典中,有相同值的键的列表
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    private List<List<string>> FindSameValueItems(Dictionary<string, HashSet<string>> src)
    {
        // 使用反向字典,原理是,以值为键,如果值相同,那么加入字典时就会发现已经存在,从而找到重复值
        var valueToKeys = new Dictionary<HashSet<string>, List<string>>(HashSet<string>.CreateSetComparer());
        foreach (var k in src)
        {
            if (!valueToKeys.TryGetValue(k.Value, out var keys))
            {
                keys = [];
                valueToKeys[k.Value] = keys;
            }
            keys.Add(k.Key);
        }
        var duplicates = new List<List<string>>();
        foreach (var d in valueToKeys)
        {
            if (d.Value.Count > 1)
                duplicates.Add(d.Value);
        }
        return duplicates;
    }
    #endregion

    #region Unit Test
    /// <summary>
    /// 返回一个规则值的处理方法,测试规则值是否生成正确
    /// </summary>
    /// <param name="clsName"></param>
    /// <returns></returns>
    public static Func<string, string> UT_GetRulesProcMethod(string clsName)
    {
        return rulesProcMethod[clsName];
    }
    #endregion
}
