using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NGinnBPM.MessageBus.Impl.Sagas;
using Moq;

namespace NGinnBPM.MessageBus.UnitTests
{
    [TestFixture]
    class SagaTests
    {

        [Test]
        public static void SagaTest1()
        {
            SagaStateHelper hh = new SagaStateHelper();
            var repo = new Mock<ISagaRepository>();
            
            hh.SagaStateRepo = repo.Object;
            hh.UseDbRecordLocking = true;
            var m = new SagaMessage1 {
                Id = "T1"
            };
            var sh = new TestSaga();
            hh.DispatchToSaga(null, m, true, true, sh, sb =>
            {
            });
        }


    }
}
