using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
internal class Helps
{
    /// <summary>
    /// 常用css单位标识. (p表示百分比%)
    /// </summary>
    public static string[] CssUnit
    {
        get { return ["p", "rem", "em", "px", "vw", "vh"]; }
    }

    /// <summary>
    /// 媒体查询前缀和屏幕尺寸分界定义
    /// </summary>
    private static Dictionary<string, string> mediaqueryData = new()
    {
        ["sm-"] = "@media (max-width: 639.9px)",
        ["md-"] = "@media (min-width: 640px) and (max-width: 991.9px)",
        ["lg-"] = "@media (min-width: 992px) and (max-width: 1199.9px)",
        ["xl-"] = "@media (min-width: 1200px)"
    };
    /// <summary>
    /// 媒体查询类名前缀 sm- md- lg- xl-
    /// </summary>
    public static string[] MediaPre
    {
        get { return [.. mediaqueryData.Keys]; }
    }

    /// <summary>
    /// 获取媒体查询定义语句,根据前缀.例如"sm-",返回 "@media (max-width: 639.9px)"
    /// </summary>
    /// <param name="mqPre"></param>
    /// <returns></returns>
    public static string GetMediaQueryDefine(string mqPre)
    {
        if (mediaqueryData.TryGetValue(mqPre, out var d))
        {
            return d;
        }
        return string.Empty;
    }

    /// <summary>
    /// 返回一个媒体查询前缀名,如果参数字符串含有的情况下.例:"sm-"返回sm,"sm"返回空
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static string GetMediaPre(string val)
    {
        foreach (var item in MediaPre)
        {
            if (val.StartsWith(item, StringComparison.OrdinalIgnoreCase))
                return item[..^1];
        }
        return string.Empty;
    }

    /// <summary>
    /// 将一个约定规则的字符串,转为合法的css规则值.如果转换失败,返回string.Empty
    /// 例如:"1"~"1px", "1rem"~"1rem", "12p"~"12%", "d5em"~"0.5em"
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static string ToCssRuleValue(string val)
    {
        // 纯整数情况
        if (int.TryParse(val, out _))
            return $"{val}px";
        // 带单位情况
        string u = string.Empty, v = string.Empty;
        foreach (string cssu in CssUnit)
        {
            if (val.EndsWith(cssu, StringComparison.OrdinalIgnoreCase))
            {
                u = cssu;
                v = val.TrimEnd(cssu.ToCharArray());
                // p结尾的是百分比单位,%
                if (u.Equals("p", StringComparison.OrdinalIgnoreCase))
                    u = "%";
                break;
            }
        }
        // 检查单位
        if (u == string.Empty)
            return string.Empty;
        // 检查值
        // 1.整数
        if (int.TryParse(v, out _))
            return $"{v}{u}";
        // 2.小数 d开头的是小数,0.
        if (v.StartsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(v[1..], out _))
                return $"0.{v[1..]}{u}";
        }
        return string.Empty;
    }

    /// <summary>
    /// 将一个约定规则的字符串,转为grid布局的grid-template-columns规则的值.如果转换失败,返回string.Empty
    /// 规则:每列宽定义用_线分割,支持auto/p(百分比)/px/rem/em/vw/vh单位,不支持小数点.
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static string ToGridTemplateColsValue(string val)
    {
        // 例:"10p_20p_70p" "200px_auto"
        string[] parts = val.Split('_');
        // 只定义了1列,而且用百分比单位
        if (parts.Length == 1)
        {
            // 1列百分比单位,自动补成2列,例如10p => "10% 90%"
            if (parts[0].EndsWith("p", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[0][..^1], out int v))
                {
                    if (v < 100)
                        return $"{v}% {100 - v}%";
                }
            }
        }
        //
        StringBuilder reVal = new();
        for (int i = 0; i < parts.Length; i++)
        {
            string item = parts[i];
            // auto值,合法
            if (item.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                reVal.Append($"{item} ");
                continue;
            }
            // 其它单位值转换
            string itemVal = ToCssRuleValue(item);
            if (itemVal == string.Empty)
                return string.Empty;
            reVal.Append($"{itemVal} ");
        }
        return reVal.ToString();
    }

    /// <summary>
    /// 合并文件并且保存到路径.采用UTF8带BOM,前3字节是标识239/187/191
    /// </summary>
    /// <param name="path"></param>
    /// <param name="bytes"></param>
    public static void UnionFiles(string path, params byte[][] bytes)
    {
        byte[] contentBytes = UnionFiles(bytes);
        File.WriteAllBytes(path, contentBytes);
    }

    /// <summary>
    /// 合并文件,返回合并后的字节.采用UTF8带BOM,前3字节是标识239/187/191.
    /// 所以要求文件字节必须是UTF8编码的.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static byte[] UnionFiles(params byte[][] bytes)
    {
        using MemoryStream ms = new();
        ms.Write([239, 187, 191]);
        foreach (byte[] item in bytes)
        {
            // 去掉UTF8签名字节(3个:239,187,191)
            if (item[0] == 239 && item[1] == 187 && item[2] == 191)
                ms.Write(item.AsSpan()[3..]);
            else
                ms.Write(item);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// 合并文件.编码为utf8
    /// </summary>
    /// <param name="fileContents"></param>
    /// <returns></returns>
    public static string UnionFiles(params string[] fileContents)
    {
        StringBuilder buf = new();
        foreach (var cssItem in fileContents)
        {
            buf.AppendLine(cssItem);
        }
        return buf.ToString();
    }
    /// <summary>
    /// 合并文件,并且保存到路径.编码为utf8
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileContents"></param>
    public static void UnionFiles(string path, params string[] fileContents)
    {
        string content = UnionFiles(fileContents);
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    /// <summary>
    /// 返回一个列表集合,元素是,源字典中,有相同值的键的列表
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public static List<List<string>> FindSameValueItems(Dictionary<string, HashSet<string>> src)
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

    /// <summary>
    /// dupKeys:在src中具有相同值的键的列表.src:源字典
    /// 该方法将src中所有相同值的元素删除,只留下一个元素:值不变,键是相同值的键的,以逗号合并的字符串
    /// 例: src中有a{color:red},b{color:red},将删除ab,加入 a,b{color:red}元素.用于合并有相同css规则的样式.
    /// </summary>
    /// <param name="dupKeys"></param>
    /// <param name="src"></param>
    public static void CombineKeyRemoveEqualValue(List<string> dupKeys, Dictionary<string, HashSet<string>> src)
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