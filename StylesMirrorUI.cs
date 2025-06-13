using AngleSharp.Css.Dom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
/// <summary>
/// mirrorui组件列表
/// </summary>
enum Uicoms
{
    all,
    allNoTheme,
    btn,
    cachepage,
    datepick,
    docmenu,
    form,
    list,
    menubar,
    mnavmenu,
    msgbox,
    msgshow,
    onoff,
    pagenum,
    range,
    sidemenu,
    table,
    tabs
}
/// <summary>
/// 预定义复杂样式文件
/// </summary>
internal class StylesMirrorUI
{
    /// <summary>
    /// 样式文件根目录
    /// </summary>
    private static string rootDir = "D:\\Mirror\\Project_git\\webcoms\\mirrorui\\stylusOutCss\\";

    /// <summary>
    /// 获取组件样式--文本结果
    /// </summary>
    /// <param name="uiNames"></param>
    /// <returns></returns>
    public static string GetCss(params Uicoms[] uiNames)
    {
        StringBuilder buf = new();
        foreach (var item in uiNames)
        {
            string path = Path.Combine(rootDir, item.ToString()) + ".css ";
            buf.AppendLine(File.ReadAllText(path));
        }
        return buf.ToString();
    }

    /// <summary>
    /// 获取组件样式--字节结果
    /// </summary>
    /// <param name="uiNames"></param>
    /// <returns></returns>
    public static byte[] GetBytesCss(params Uicoms[] uiNames)
    {
        byte[] buf = [];
        foreach (var item in uiNames)
        {
            string path = Path.Combine(rootDir, item.ToString()) + ".css ";
            buf = [.. buf, .. File.ReadAllBytes(path)];
        }
        return buf;
    }
}
