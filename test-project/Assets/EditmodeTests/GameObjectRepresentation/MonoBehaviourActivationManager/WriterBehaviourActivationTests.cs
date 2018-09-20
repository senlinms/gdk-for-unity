using NUnit.Framework;
using UnityEngine;

namespace Improbable.Gdk.GameObjectRepresentation.EditModeTests.MonoBehaviourActivationManagerTests
{
    [TestFixture]
    public class WriterBehaviourActivationTests :
        AuthorityRequiredBehaviourActivationTestsBase<WriterBehaviourActivationTests.TestBehaviourWithWriter>
    {
        protected override uint ComponentId => new Position.Component().ComponentId;

        protected override void ValidateRequirablesNotNull()
        {
            Assert.IsNotNull(TestGameObject.GetComponent<TestBehaviourWithWriter>().PositionWriter);
        }

        public class TestBehaviourWithWriter : MonoBehaviour
        {
            [Require] public Position.Requirable.Writer PositionWriter;
        }
    }
}
