using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UsedLessCss;

// 基本测试
//Test();
//TestFileWatch();

// 为项目生成样式
var cfg = new
{
    uidoc = "_mirrorUiCfg",
    icondoc = "_mirrorIconCfg",
    blog = "_blogCfg"
};
ForProject($"usedCssCfgForProject/{cfg.blog}.json");

static void Test()
{
    string input = "test/index.html";
    string output = "D:\\Mirror\\Project_git\\UsedLessCss\\test\\index.css";
    string outcss = new UsedCss().Run(input);
    File.WriteAllText(output, outcss);
}

// 文件监控
static void TestFileWatch()
{
    var srv = new UsedCss();
    string output = "E:\\WWWROOT\\staticsite/index.css";
    // 文件监测功能初始化
    FileSystemWatcher watcher = new();
    watcher.Path = "D:\\mirrortom.github.io\\mirrorui\\doc";
    watcher.NotifyFilter = NotifyFilters.LastWrite;
    watcher.Filter = "*.html";
    watcher.Changed += (object sender, FileSystemEventArgs e) =>
    {
        // 事件首次出发时,关掉监测,避免事件多次触发
        watcher.EnableRaisingEvents = false;
        //
        Console.WriteLine("文件: " + e.FullPath + " " + e.ChangeType);
        var css = srv.Run(e.FullPath);
        File.AppendAllText(output, css);

        // 事件执行完成后,再次打开监测
        watcher.EnableRaisingEvents = true;
    };
    // 开始监视
    watcher.EnableRaisingEvents = true;

    Console.WriteLine("正在监视 " + watcher.Path + " 目录的文件变化...");
    Console.WriteLine("按 'q' 键退出监视程序。");

    // 保持程序运行
    while (Console.Read() != 'q') ;
}

// 为项目生成样式.每个项目有配置文件,传入对应配置.
static void ForProject(string cfgPath)
{
    // 1.配置读取
    dynamic cfg = JsonConvert.DeserializeObject(File.ReadAllText(cfgPath));
    if (cfg == null) return;
    // 2.html文件
    List<string> files = [];
    foreach (var file in cfg.htmlFiles)
    {
        if (file.path != null)
        {
            files.Add((string)file.path);
        }
        else if (file.dir != null)
        {
            if (file.fileNames != null)
            {
                foreach (string fName in file.fileNames)
                {
                    files.Add(Path.Combine((string)file.dir, fName));
                }
                continue;
            }
            files.AddRange(Directory.GetFiles((string)file.dir));
        }
    }
    // 3.静态样式选择
    List<byte[]> cssFileBuf = [];
    foreach (var style in cfg.styles)
    {
        if (style.path != null)
        {
            cssFileBuf.Add(File.ReadAllBytes((string)style.path));
        }
        else if (style.dir != null)
        {
            if (style.fileNames != null)
            {
                foreach (string fName in style.fileNames)
                {
                    string path = Path.Combine((string)style.dir, fName);
                    cssFileBuf.Add(File.ReadAllBytes(path));
                }
                continue;
            }
            foreach (var cssfile in Directory.GetFiles((string)style.dir))
            {
                cssFileBuf.Add(File.ReadAllBytes(cssfile));
            }
        }
    }
    // 4.处理
    var uc = new UsedCss();
    // 4.1忽略的 样式类列表
    if (cfg.ignoreClass != null)
    {
        var ignoreClass = File.ReadAllText((string)cfg.ignoreClass);
        uc.IgnoreClassLoad(ignoreClass.Split(Environment.NewLine));
    }
    // 4.2明确加入的
    if (cfg.explicitlyClass != null)
    {
        var explicitlyClass = File.ReadAllText((string)cfg.explicitlyClass);
        uc.ExplicitlyClassListLoad(explicitlyClass.Split(Environment.NewLine));
    }
    string css = uc.Run([.. files]);
    cssFileBuf.Add(Encoding.UTF8.GetBytes(css));
    // 5.合并输出
    string outPath = cfg.OutputCss;
    Helps.UnionFiles(outPath, [.. cssFileBuf]);
}