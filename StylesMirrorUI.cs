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
    /// 获取组件样式
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    public static string GetCss(Uicoms uiName)
    {
        string path = Path.Combine(rootDir, uiName.ToString()) + ".css ";
        return File.ReadAllText(path);
    }

}
