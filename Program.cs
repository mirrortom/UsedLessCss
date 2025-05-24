using System.IO;
using UsedLessCss;
// 基本测试
Test();
//TestFileWatch();

static void Test()
{
    string input = "test/index.html";
    string runPath = AppDomain.CurrentDomain.BaseDirectory;
    string output = runPath[..runPath.IndexOf("bin")] + "test/index.css";
    new UsedCss().Run(input, output, false);
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
        var css = srv.Run(e.FullPath, false);
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