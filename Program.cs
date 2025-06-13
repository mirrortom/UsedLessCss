using System.IO;
using System.Text;
using UsedLessCss;
// 基本测试
//Test();
//TestFileWatch();
//ForMirrorUiDoc(); 
ForMirrorIconDoc();
//var m=UsedCss.UT_GetRulesProcMethod("0");
//Console.WriteLine(m("13"));

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

// 为mirrorui项目文档生成样式
static void ForMirrorUiDoc()
{
    // 1.静态样式文件选取
    byte[] globle = StylesGlobal.GetBytesCss();
    byte[] uiAll = StylesMirrorUI.GetBytesCss(Uicoms.all);
    // 2.html文件
    string[] filePaths = Directory.GetFiles("D:\\Mirror\\Project_git\\webcoms\\mirrorui\\doc\\");
    List<string> files = new();
    files.Add("D:\\Mirror\\Project_git\\webcoms\\mirrorui\\index.html");
    files.AddRange(filePaths);
    // 3.忽略类/明确添加类 列表
    string ignoreClass = File.ReadAllText("usedCssCfg/mirrorui_ignoreClass.txt");
    string explicitlyClass = File.ReadAllText("usedCssCfg/mirrorui_explicitlyClass.txt");
    // 4.生成
    var uc = new UsedCss();
    uc.IgnoreClassLoad(ignoreClass.Split(Environment.NewLine));
    uc.ExplicitlyClassListLoad(explicitlyClass.Split(Environment.NewLine));
    string css = uc.Run([.. files]);
    // 5.合并多个样式文件
    string outPath = "D:\\Mirror\\Project_git\\UsedLessCss\\outcss\\uidoc.css";
    Helps.UnionFiles(outPath, globle, uiAll, Encoding.UTF8.GetBytes(css));
}

// 为webicon文档生成样式
static void ForMirrorIconDoc()
{
    // 1.静态样式文件选取
    byte[] globle = StylesGlobal.GetBytesCss();
    byte[] mirrorui = StylesMirrorUI.GetBytesCss(Uicoms.btn, Uicoms.cachepage);
    byte[] iconcss = StylesMirrorIcon.GetBytesCss();
    // 2.html文件
    string[] filePaths = Directory.GetFiles("D:\\Mirror\\Project_git\\webicons\\mirroricon\\doc\\");
    List<string> files = new();
    files.Add("D:\\Mirror\\Project_git\\webicons\\mirroricon\\index.html");
    files.AddRange(filePaths);
    // 3.忽略类/明确添加类 列表
    string ignoreClass = File.ReadAllText("usedCssCfg/mirrorIcon_ignoreClass.txt");
    //string explicitlyClass = File.ReadAllText("usedCssCfg/mirrorui_explicitlyClass.txt");
    // 4.生成
    var uc = new UsedCss();
    uc.IgnoreClassLoad(ignoreClass.Split(Environment.NewLine));
    //uc.ExplicitlyClassListLoad(explicitlyClass.Split(Environment.NewLine));
    string css = uc.Run([.. files]);
    // 5.合并多个样式文件
    string outPath = "D:\\Mirror\\Project_git\\UsedLessCss\\outcss\\icondoc.css";
    Helps.UnionFiles(outPath, globle, mirrorui, iconcss, Encoding.UTF8.GetBytes(css));
}