using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using System.Collections.Immutable;
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
        // grid布局,值为列数,平均分配列宽
        ["grid-cols"] = (val) =>
        {
            if ((int.TryParse(val, out int _)))
            {
                return val;
            }
            return string.Empty;
        },
        // grid布局,值为每列列宽大小,支持像素/百分比/vw,格式 grid-[num](p|px|vw)[num](p|px|vw)
        ["grid"] = (val) =>
        {
            return Helps.ToGridTemplateColsValue(val);
        },
        // 通用规则 规则key为0,避免和class类名冲突.
        ["0"] = (val) =>
        {
            return Helps.ToCssRuleValue(val);
        }
    };

    /// <summary>
    /// 匹配成功的css样式的缓存器,键是选择器,值是规则集合,HashSet每个元素是一个规则
    /// 使用这个特点的集合可以去掉重复的选择器和重复的规则
    /// </summary>
    private Dictionary<string, HashSet<string>> buffer;

    /// <summary>
    /// 匹配成功的css样式缓存器,但是属于媒体查询的,样式类名前缀是sm-,md-,lg-,xl-
    /// 媒体查询样式单独处理,要放在其它样式的后面
    /// </summary>
    private Dictionary<string, HashSet<string>> bufferMediaquery;

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
        simpleRules ??= RulesSimpleLoad("classRuleMap/simpleRules.ini");
        // 载入简化类名和实际类名字典
        styleNamesSimple ??= RulesSimpleLoad("classRuleMap/styleSimpleName.ini");
        // 载入css预定义值
        styleValues ??= RulesSimpleLoad("classRuleMap/styleValues.ini");
    }

    /// <summary>
    /// htmlContent:html文件内容.
    /// 多个css文件合并时要注意顺序,生成的工具样式要放到最后,由于选择器只有1个类选择器,优先级容易被其它组合class超过.
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
        CombineEqualRules();

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
        bufferMediaquery = new(StringComparer.OrdinalIgnoreCase);
#if DEBUG
        Console.WriteLine($"总共{classes.Count}个样式需要匹配");

        Console.WriteLine();
        int successCount = 0;
        int codeIndex = 1;
#endif
        foreach (var clsName in classes)
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
                if (classFileIdMap.TryGetValue(clsName, out var fArr))
                {
                    foreach (var fIndex in fArr)
                    {
                        logBuf.Append($"[{Path.GetFileName(inputFiles[fIndex])}], ");
                    }
                }
                else
                {
                    logBuf.Append("[无.该样式类来自明确加入.]");
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
        this.ignoreClassList = [];
        foreach (var item in clsNames)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;
            this.ignoreClassList.Add(item);
        }
    }

    /// <summary>
    /// 明确加入样式列表
    /// </summary>
    /// <param name="clsNames"></param>
    public void ExplicitlyClassListLoad(params string[] clsNames)
    {
        if (this.explicitlyClassList != null) return;
        this.explicitlyClassList = [];
        foreach (var item in clsNames)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;
            this.explicitlyClassList.Add(item);
        }
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
            AddBuffer(clsName, match);
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
    /// 生成逻辑:样式类简化名字,包含了规则名字和规则值信息,分析后生成css规则.
    /// 简化类名约定格式: [媒体查询-]pre[-规则名字][-规则值名字]-val pre为前缀,val为值
    /// 例1: "bd-red-7","bd-red"转为为规则名:"border-color","red-7"转为规则值:"#b91c1c"
    /// 例2: "pd-20","pd"转规则名:"padding","20"转为值:"20px"
    /// </summary>
    /// <param name="clsName"></param>
    /// <returns></returns>
    bool DynamicRuleValueMatch(string clsName)
    {
        // 1.分析规则名字
        var clsArr = clsName.Split('-');
        // 分析规则名字.例: mg-20, grid-cols-3, sm-grid-cols-3(媒体查询版本)
        if (clsArr.Length < 2)
            return false;
        // 规则名字前缀key不含媒体查询前缀部分(第1个)如果有,不含值部分(最后1个)
        int startIndex = Helps.GetMediaPre(clsName) == string.Empty ? 0 : 1;
        string preKey = string.Join('-', clsArr[startIndex..^1]);
        // 到字典找出对应类全名. 示例: mg => margin:$v. 
        if (!styleNamesSimple.TryGetValue(preKey, out var match))
            return false;

        // 2.分析规则值
        string ruleTypeName = clsArr[^2];
        string ruleValue = clsArr[^1];
        string targetVal = string.Empty;

        // 到字典找出预定义值
        if (styleValues.TryGetValue($"{ruleTypeName}{ruleValue}", out var matchValue))
        {
            targetVal = matchValue;
        }
        // 非预定义值时
        else if (rulesProcMethod.TryGetValue(preKey, out var method))
        {
            // 特定的规则处理
            targetVal = method(ruleValue);
        }
        else
        {
            // 一般性规则处理
            targetVal = rulesProcMethod["0"](ruleValue);
        }

        // 值不符合要求
        if (targetVal == string.Empty)
            return false;

        // 替换为实际值,加入到输出缓存 match示例: margin:$v或者margin-left:$v;margin-right:$v
        string rules = match.Replace("$v", targetVal);
        AddBuffer(clsName, rules);
#if DEBUG
        Console.WriteLine($"简单动态值匹配成功 {clsName}");
        logBuf.AppendLine($"简单动态值匹配-成功^_^!");
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
    void AddBuffer(string name, string val)
    {
        var buf = buffer;
        // 如果是媒体查询样式,加入到对应buffer
        if (Helps.GetMediaPre(name) != string.Empty)
        {
            buf = bufferMediaquery;
        }
        string clsN = name.StartsWith("hover-", StringComparison.OrdinalIgnoreCase) ?
        $"{name}:hover" : name;

        // 相同的class名字,合并成一个样式
        if (!buf.TryGetValue(name, out var _))
        {
            buf[clsN] = [];
        }
        // hashset集合,避免加入相同规则
        var items = val.Split(';');
        foreach (var c in items)
        {
            // 拆解规则键值对,去掉空客,统一格式.避免相同规则重复加入.
            // 例如"left:0"和"left: 0"或" left:0"
            var r = c.Split(":");
            buf[clsN].Add($"{r[0].Trim()}:{r[1].Trim()}");
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
        StringBuilder strBuf = new();
        strBuf.AppendLine("/*-----usedCss--split--line-----*/");
        // 加入普通样式集
        foreach (var k in buffer.Keys)
        {
            FomartToTxtBuf(strBuf, k, buffer[k]);
        }
        // 加入媒体样式集 
        // 先根据几种媒体类型分组,再按组添加.
        Dictionary<string, string[]> mqGroupKeys = [];
        // mqType: sm- md- lg- xl-
        foreach (var mqType in Helps.MediaPre)
        {
            mqGroupKeys.Add(mqType, bufferMediaquery.Keys.Where(o => Helps.GetMediaPre(o) == mqType[..^1]).ToArray());
        }
        foreach (var gKeys in mqGroupKeys)
        {
            // 如果这个媒体类型下有规则,才加入
            if (gKeys.Value != null && gKeys.Value.Length > 0)
            {
                // 媒体查询尺寸分界点定义语句@media xxx
                strBuf.Append(Helps.GetMediaQueryDefine(gKeys.Key));
                strBuf.AppendLine(" {");
                foreach (var k in gKeys.Value)
                {
                    FomartToTxtBuf(strBuf, k, bufferMediaquery[k], 4);
                }
                strBuf.AppendLine("}");
            }
        }
#if DEBUG
        strBuf.AppendLine($"/*-----usedCss-----Create time: {DateTime.Now.ToString()}-----*/");
#endif
        return strBuf.ToString().TrimEnd();
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
        // 类名.(如果是写在媒体查询下的样式,需要增加缩进,indentCount=4)
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
    private void CombineEqualRules()
    {
        // 遍历缓存,查询含有完全相同规则的样式,合并成一个样式
        // 普通样式集 duplicates:相同的
        var dupGroupList = Helps.FindSameValueItems(this.buffer);
        if (dupGroupList.Count > 0)
        {
            // 遍历重复键集合
            foreach (var dupKeys in dupGroupList)
            {
                Helps.CombineKeyRemoveEqualValue(dupKeys, this.buffer);
            }
        }
        // 媒体查询样式合并,例如:.sm-block{display:block}和md-block{display:none}
        // 规则一样,但属于不同的媒体查询分段,不能合成为sm-block,md-block{display:none}
        // 必须规则一样而且前缀也一样才能合并:sm-a-red{color:red},sm-b-red:{color:red}

        // 实际情况是,规则一样,但取不同类名,大多情况下都是多余的做法.所以,这个方法很少有用.
    }

    #endregion

    #region Unit Test
    
    #endregion
}
