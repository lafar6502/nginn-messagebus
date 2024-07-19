using NGinnBPM.DSLServices;
using System.IO;

namespace NginnBPM.DSLServices.Tests
{
    public class DslTests
    {
        private SimpleBaseClassDslCompiler<MyScriptBase> _dsl;

        [SetUp]
        public void Setup()
        {
            var bdir = AppDomain.CurrentDomain.BaseDirectory;
            var pth = Path.Combine(bdir, "TestScripts");
            Console.WriteLine("Script base dir is " + pth);
            _dsl = new SimpleBaseClassDslCompiler<MyScriptBase>(new SimpleFSStorage(pth, false));
            _dsl.CompilationCallback((cc, urls) =>
            {
                Console.WriteLine("Compilation of {0}", string.Join(", ", urls));
            });
        }

        [Test]
        public void Test1()
        {
            var alls = _dsl.CreateAll();
            foreach(var x in alls)
            {
                Console.WriteLine("{0}", x.GetType().Name);
            }
        }

        [Test]
        public void Test2()
        {
            var alls = _dsl.CreateAll();
            foreach (MyScriptBase x in alls)
            {
                Console.WriteLine("{0}", x.GetType().Name);
                x.Prepare();
            }
        }
    }
}