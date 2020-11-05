using System.Web;
using System.Web.Mvc;

namespace md.akharinkhabar.ir
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}