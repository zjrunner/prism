using System.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Prism.Views
{
    public class DiagnosticsModel : PageModel
    {
        private RequestLog _requestLog;

        public DiagnosticsModel(RequestLog reqeustLog)
        {
            _requestLog = reqeustLog;
        }

        public void OnGet()
        {
            ViewData["RequestLog"] = _requestLog.GetTail(50).ToArray();
        }
    }
}