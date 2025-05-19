using UsedLessCss;
string input = "testHtml/index.html";
string runPath = AppDomain.CurrentDomain.BaseDirectory;
string output = runPath[..runPath.IndexOf("bin")] + "testOutCss/index.css";
new UsedCss().Run(input, output);