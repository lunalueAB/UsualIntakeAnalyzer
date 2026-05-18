using System.Text;
using System.Windows;

namespace UsualIntakeAnalyzer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // CP949(EUC-KR) 등 확장 코드페이지 지원 — CSV 인코딩 감지에 필요
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            base.OnStartup(e);
            Services.AppDataService.Initialize();
        }
    }
}
