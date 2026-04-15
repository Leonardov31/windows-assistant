using WindowsAssistant.UI;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

using var app = new TrayApplication();
Application.Run(app);
