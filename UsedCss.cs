using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using System.Linq;
using System.Text;

namespace UsedLessCss;
/*
 *  目标功能: 动态生成css,输出按需使用到的css样式
 *  
 *  css生成有2种方式,第一种是从预定义的css文件里,根据.class|#id(不支持)|[prop](不支持)查找对应的css规则,
 *  第2种方式是根据约定的class名字动态生成css.例如类名 "mg-l-20",会生成 margin-left:20px这个规则
 *  第2种方法是第1种方法的补充,主要对于常用的margin|padding之类的,而且取值很广泛的那种情况.
 *  
 *  使用方式:new UsedCss().Run("index.html", "output.css");
 *  
 *  流程:
 *  1.载入规则集
 *  2.取出html中所有使用到的class
 *  3.遍历class,到所有规则集匹配,抽取css样式
 *  4.合并相同规则集的样式
 *  5.输出css文件
 *  
 *  其它细节看对应注释
 *  
 *  还要实现功能:
 *  1.css变量规则集
 *  2.功能集成到VS插件
 */

internal class UsedCss
{
    /// <summary>
    /// 简单css预定义规则集合,主要是工具样式,使用单个类选择器,规则数目主要是1条的,也有少量多条的 
    /// </summary>
    private static Dictionary<string, string> simpleRules;

    /// <summary>
    /// 复合的css预定义规则集合,含有多种选择器,选择器和规则的个数大多数在1条以上
    /// </summary>
    private static ICssStyleSheet complexRules;

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
    /// 全局css样式 
    /// </summary>
    private static string baseCss;


    /// <summary>
    /// 特定css规则处理集合,返回样式的值.键是简化类名,值是处理方法
    /// 0:一般性处理,返回像素px或者百分比%单位的值.
    /// 其它特别处理方法在此加入
    /// </summary>
    private static Dictionary<string, Func<string, string>> rulesProc = new()
    {
        // 通用规则 键名特别取值,不使用class类名.class名字不能数字开头,不能含有%,#等特殊符号
        { "0" ,
            (val) =>
            {
                if ((int.TryParse(val, out int _)))
                {
                    // 纯数值默认是像素单位
                    return $"{val}px";
                }
                string[] unit = ["rem", "vw", "vh", "px"];
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
        }
    };

    /// <summary>
    /// 匹配成功的css样式的缓存器,键是选择器,值是规则集合,HashSet每个元素是一个规则
    /// 使用这个特点的集合可以去掉重复的选择器和重复的规则
    /// </summary>
    private Dictionary<string, HashSet<string>> buffer;

    /// <summary>
    /// 匹配成功时的媒体查询css样式的缓存器,键是媒体查询名,值是里面的样式规则(同buffer)
    /// </summary>
    private Dictionary<string, Dictionary<string, HashSet<string>>> bufferMedia;

    /// <summary>
    /// 初始化对象时载入了数据,请调用Run方法完成任务.
    /// </summary>
    public UsedCss()
    {
        // 初始化规则集 使用静态成员,避免多次初始化
        simpleRules ??= RulesSimpleLoad("dataDefault/simpleRules.ini");
        complexRules ??= RulesComplexLoad("dataDefault/complexRule.css");
        // 载入简化类名和实际类名字典
        styleNamesSimple ??= RulesSimpleLoad("dataDefault/styleSimpleName.ini");
        // 载入css预定义值
        styleValues ??= RulesSimpleLoad("dataDefault/styleValues.ini");
        // 载入全局css
        baseCss ??= BaseCssLoad("dataDefault/globalBase.css");
    }

    /// <summary>
    /// input:html文件路径,output:css输出路径
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    public void Run(string input, string output = "output.css")
    {
        // 1. 读取HTML文件
        var htmlContent = File.ReadAllText(input);

        // 2. 使用AngleSharp解析HTML并提取class
        var parser = new HtmlParser();
        var document = parser.ParseDocument(htmlContent);

        var classes = ExtractClasses(document);

        // 3. 生成CSS
#if DEBUG
        Console.WriteLine("CSS开始生成");
#endif
        GenerateCss(classes);

        // 4.合并相同样式
        SameRulesCombine();

        // 5. 输出CSS文件
        File.WriteAllText(output, ToCssTxt());
#if DEBUG
        Console.WriteLine("CSS文件生成完成!");
        Console.WriteLine();
#endif
    }

    #region 提取html的class

    /// <summary>
    /// 提取所有元素的class属性
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    HashSet<string> ExtractClasses(AngleSharp.Dom.IDocument document)
    {
        // HashSet不会插入重复元素,避免提取相同的类名字
        HashSet<string> classes = new(StringComparer.OrdinalIgnoreCase);

        // 提取所有元素的class属性
        foreach (var element in document.All)
        {
            if (element.ClassList.Length > 0)
            {
                foreach (var className in element.ClassList)
                {
                    classes.Add(className.Trim());
                }
            }
        }

        return classes;
    }

    /// <summary>
    /// 匹配抽取css
    /// </summary>
    /// <param name="classes"></param>
    void GenerateCss(HashSet<string> classes)
    {
        buffer = [];
        bufferMedia = [];
#if DEBUG
        Console.WriteLine($"总共{classes.Count}个样式需要匹配");
        Console.WriteLine();
        int count = 0;
#endif
        foreach (var clsName in classes.OrderBy(c => c))
        {
#if DEBUG
            Console.WriteLine($"开始匹配: {clsName}");
#endif
            bool isok =
            // 简单预定义css匹配
            SimpleRuleMatch(clsName) ||
            // 简单动态值匹配
            DynamicRuleValueMatch(clsName) ||
            // 复杂预定义css匹配
            ComplexRuleMatch(clsName);

#if DEBUG
            if (isok == false)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"匹配失败{count}！ {clsName}");
                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.WriteLine();
            count++;
#endif
        }
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
    private Dictionary<string, string> RulesSimpleLoad(string file)
    {
        // from rule file 
        string rulesStr = File.ReadAllText(file);
        var ruleLines = rulesStr.Split(Environment.NewLine);
        // load rule in dict
        Dictionary<string, string> rules = new();
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
                    rules.Add(k.Trim(), rule[1].Trim());
            }
        }
        //
        return rules;
    }

    /// <summary>
    /// 复杂预定义css规则集载入
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private ICssStyleSheet RulesComplexLoad(string file)
    {
        // 解析预定义复杂css文件
        var parser = new CssParser();
        // 缓存stylesheet
        return parser.ParseStyleSheet(File.ReadAllText(file));
    }

    /// <summary>
    /// 载入全局css
    /// </summary>
    /// <param name="cssPath"></param>
    string BaseCssLoad(string cssPath)
    {
        return File.ReadAllText(cssPath);
    }
    #endregion


    #region 匹配,从几种规则集中取出规则

    /// <summary>
    /// 复杂预定义css规则匹配.源文件是一个合格的css文件,包含各种类型的样式定义.
    /// 比如常规的class/css变量/媒体查询/css动画等,每个定义下一般都含有多条css规则.
    /// 建议css文件不要有重复定义.相同的CSS规则,写成一个css对象.
    /// all: ture全员遍历,false找到第一个退出
    /// </summary>
    /// <param name="clsName"></param>
    /// <returns></returns>
    bool ComplexRuleMatch(string clsName, bool all = true)
    {
        bool isMatched = false;
        int mediaRulecount = 0;
        // 遍历样式源,查找指定类选择器.源中可能有重复的类,所以要全部遍历.
        foreach (var rule in complexRules.Rules)
        {
            // 一般样式
            if (rule is AngleSharp.Css.Dom.ICssStyleRule styleRule)
            {
                // 源样式类选择器(样式选择器可能是多个,是用,号分割的)
                string[] arr = styleRule.SelectorText.Split(',');
                // 如果源样式名含有目标样式名字,则提取这个样式里的规则.
                if (arr.Contains('.' + clsName, StringComparison.OrdinalIgnoreCase))
                {
                    AddBuffer(clsName, styleRule.Style.CssText, this.buffer);
                }
                isMatched = true;
            }
            // 媒体查询样式
            else if (rule is AngleSharp.Css.Dom.ICssMediaRule mediaRule)
            {
                Dictionary<string, HashSet<string>> innerRuleBuf = new();
                // 遍历里面的所有样式,比较(同一般样式的比较行为,加入到媒体样式缓存)
                foreach (var innerRule in mediaRule.Rules.OfType<ICssStyleRule>())
                {
                    // 源样式类选择器(样式选择器可能是多个,是用,号分割的)
                    string[] arr = innerRule.SelectorText.Split(',');
                    // 如果源样式名含有目标样式名字,则提取这个样式里的规则.
                    if (arr.Contains('.' + clsName, StringComparison.OrdinalIgnoreCase))
                    {
                        AddBuffer(clsName, innerRule.Style.CssText, innerRuleBuf);
                        mediaRulecount++;
                    }
                }
                // 如果该媒体查询下面,没有一个样式匹配中,不需要加入该媒体查询
                if (mediaRulecount > 0)
                {
                    AddMediaBuffer(mediaRule.ConditionText.Trim(), innerRuleBuf);
                }
            }
        }
#if DEBUG
        if (mediaRulecount > 0)
            Console.WriteLine($"媒体查询预定义匹配 {clsName} 成功!包含样式个数{mediaRulecount}.");
        else if (isMatched)
            Console.WriteLine($"一般复杂预定义匹配 {clsName} 成功!");
#endif
        return isMatched;
    }



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
            Console.WriteLine($"简单预定义匹配 {clsName} 成功!");
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
            if (rulesProc.TryGetValue(preName, out var method))
            {
                // 特定的规则处理
                targetVal = method(ruleValue);
            }
            else
            {
                // 一般性规则处理
                targetVal = rulesProc["0"](ruleValue);
            }
        }
        // 值不符合要求
        if (targetVal == string.Empty)
            return false;

        // 替换为实际值,加入到输出缓存 match示例: margin:$v或者margin-left:$v;margin-right:$v
        string rules = match.Replace("$v", targetVal);
        AddBuffer(clsName, rules, this.buffer);
#if DEBUG
        Console.WriteLine($"简单动态匹配 {clsName} 成功!");
#endif
        return true;
    }
    #endregion


    #region 匹配成功的规则加入缓存


    /// <summary>
    /// 将一个媒体查询加入到缓存
    /// </summary>
    /// <param name="mediaName"></param>
    /// <param name="newInnerRules"></param>
    private void AddMediaBuffer(string mediaName, Dictionary<string, HashSet<string>> newInnerRules)
    {
        if (this.bufferMedia.TryGetValue(mediaName, out var _))
        {
            // 如果存在该媒体样式(一般不会将同样的媒体查询写多次,这种情况极少发生)
            // 将该媒体下的规则和新找到的规则合并.
            // 这需要操作2个字典合并.类型是:Dictionary<string, HashSet<string>>
            // 手动合并
            // 1. 遍历新找到的样式字典
            foreach (var newClassName in newInnerRules.Keys)
            {
                // 2. 如果媒体缓存中已经存在这个样式了,合并字典的值(HashSet<string>)
                if (this.bufferMedia[mediaName].TryGetValue(newClassName, out var _))
                {
                    // UnionWith()例如a.UnionWith(b),会修改a,a合并后有a和b所有元素
                    this.bufferMedia[mediaName][newClassName]
                    .UnionWith(newInnerRules[newClassName]);
                }
                else
                {
                    // 3. 加入新值
                    this.bufferMedia[mediaName].Add(newClassName, newInnerRules[newClassName]);
                }
            }
        }
        else
        {
            this.bufferMedia[mediaName] = newInnerRules;
        }
    }


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
            // 拆解规则键值对,去掉空客,同意格式.避免相同规则重复加入.
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
        // 加入全局css
        sb.AppendLine(baseCss);
        sb.AppendLine("/*===GLOBAL BASE END LINE===*/");
        sb.AppendLine();
        // 加入普通样式集
        foreach (var k in buffer.Keys)
        {
            FomartToTxtBuf(sb, k, buffer[k]);
        }
        // 加入媒体样式集
        foreach (var mk in bufferMedia.Keys)
        {
            sb.AppendLine($"@media {mk} {{");
            foreach (var k in bufferMedia[mk].Keys)
            {
                // 媒体查询下的样式需要缩进
                FomartToTxtBuf(sb, k, bufferMedia[mk][k], 4);
            }
            sb.AppendLine("}");
        }
#if DEBUG
        sb.AppendLine($"/*===生成时间Create time: {DateTime.Now.ToString()} ===*/");
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
        // 媒体样式集
        foreach (var mk in this.bufferMedia.Keys)
        {
            // 遍历每一个媒体查询样式下的所有样式,如果值有相同的,合并.逻辑处理和普通样式相同.
            var dupGroupListInner = FindSameValueItems(this.bufferMedia[mk]);
            if (dupGroupListInner.Count > 0)
            {
                // 遍历重复键集合
                foreach (var dupKeys in dupGroupListInner)
                {
                    RemoveDupAndAddNew(dupKeys, this.bufferMedia[mk]);
                }
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
}
