using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
internal class StylesMirrorIcon
{

    /// <summary>
    /// 样式文件根目录
    /// </summary>
    private static string rootDir = "D:\\Mirror\\Project_git\\webicons\\mirroricon\\";

    /// <summary>
    /// 获取图标样式--文本结果
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    public static string GetCss()
    {
        string path = Path.Combine(rootDir, "mirroricon.css");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 获取图标样式--字节结果
    /// </summary>
    /// <param name="uiNames"></param>
    /// <returns></returns>
    public static byte[] GetBytesCss()
    {
        string path = Path.Combine(rootDir, "mirroricon.css");
        return File.ReadAllBytes(path);
    }
}
