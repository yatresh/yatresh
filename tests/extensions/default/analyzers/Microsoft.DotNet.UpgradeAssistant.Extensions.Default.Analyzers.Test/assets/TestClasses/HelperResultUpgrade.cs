using System;

namespace TestProject.TestClasses
{
    public class HelperResultUpgrade
    {
        public HelperResult Method1(System.Web.WebPages.HelperResult r)
        {
            Func<string, System.Web.WebPages.HelperResult> x = a => new HelperResult(writer => writer.Write(a));

            return (Foo.HelperResult)r;
        }
    }
}
