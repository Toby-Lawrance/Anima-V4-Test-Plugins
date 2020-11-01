using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Plugins;

namespace TestPlugin
{
    public class TestModule : Module
    {

        public TestModule() : base("Test Plugin", "A test plugin",TimeSpan.FromSeconds(1)) {}

        public override void Init()
        {
            base.Init();
            var succ = Anima.Instance.KnowledgePool.TryInsertValue("Count", 0);
            if(succ) {Anima.Instance.WriteLine("Set count initial value");}

        }

        public override void Tick()
        {
            var succ = Core.Anima.Instance.KnowledgePool.TryGetValue("Count",out int Count);
            if (succ)
            {
                Anima.Instance.WriteLine($"Count:{Count}");
                var setSucc = Anima.Instance.KnowledgePool.TrySetValue("Count", Count + 1);
                if(setSucc == false) {Anima.Instance.WriteLine("Unable to increment Count");}
            }
            else
            {
                Anima.Instance.ErrorStream.WriteLine("Couldn't get Count");
            }
        }
    }


    public class TestModule2 : Module
    {
        public TestModule2() : base("Test Plugin 2", "Testing if multiple modules can be loaded from a single dll",TimeSpan.FromSeconds(3)) {}

        public override void Tick()
        {
            if (Core.Anima.Instance.MailBoxes.CheckNumMessages(this) == 0)
            {
                Core.Anima.Instance.WriteLine($"No messages for: {this.Identifier}");
            }
            while (Core.Anima.Instance.MailBoxes.CheckNumMessages(this) > 0)
            {
                Core.Anima.Instance.WriteLine(Core.Anima.Instance.MailBoxes.GetMessage(this));
            }
            
        }
    }

    public class TestModule3 : Module
    {
        public TestModule3() : base("Test Plugin 3", "Test for embedded", TimeSpan.FromSeconds(1)) { Enabled = true; }

        public override void Init()
        {
            base.Init();
            Anima.Instance.KnowledgePool.TryInsertValue("Count", 0);
        }

        public override void Tick()
        {
            var succ = Anima.Instance.KnowledgePool.TryGetValue("Count", out int Count);
            if(!succ) {return;}

            var NetMsg = new Core.Network.NetMessage(Environment.MachineName,this.Identifier,"Test Plugin 2", string.Concat(Enumerable.Repeat("a", Count)));
            var Msg = Message.CreateMessage(this, "Relay-Client", Anima.Serialize(NetMsg));
            Anima.Instance.MailBoxes.PostMessage(Msg);
        }
    }
}
