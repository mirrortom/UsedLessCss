using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
internal class StylesGlobal
{
    /// <summary>
    /// 样式文件根目录
    /// </summary>
    private static string rootDir = "D:\\Mirror\\Project_git\\UsedLessCss\\dataDefault\\";

    /// <summary>
    /// 获取样式
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    public static string GetCss()
    {
        string path = Path.Combine(rootDir, "globalBase.css");
        return File.ReadAllText(path);
    }
}
