using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.CoreData;
using Core.Plugins;
using Newtonsoft.Json;

namespace TestPlugin
{
    public class TestModule : Module
    {
        private KnowledgeBase<int> counting = new KnowledgeBase<int>();

        public TestModule() : base("Test Plugin", "A test plugin",TimeSpan.FromSeconds(1)) {}

        public override void Init()
        {
            base.Init();
            var succ = counting.TryInsertValue("Count", 0);
            if(succ) {Anima.Instance.WriteLine("Set count initial value");}

        }

        public override void Tick()
        {
            var succ = counting.TryGetValue("Count",out int Count);
            if (succ)
            {
                var msg = Message<int>.CreateMessage(this, "Test Plugin 2", Count);
                Anima.Instance.SystemMail.PostMessage(msg);
                
                Anima.Instance.WriteLine($"Count:{Count}");
                var setSucc = counting.TrySetValue("Count", Count + 1);
                if(setSucc == false) {Anima.Instance.WriteLine("Unable to increment Count");}
            }
            else
            {
                Anima.Instance.ErrorStream.WriteLine("Couldn't get Count");
            }
        }

        public override string Serialize()
        {
            return JsonConvert.SerializeObject(counting);
        }

        public override void Deserialize(string jsonData)
        {
            counting = JsonConvert.DeserializeObject<KnowledgeBase<int>>(jsonData);
        }
    }


    public class TestModule2 : Module
    {
        public TestModule2() : base("Test Plugin 2", "Testing if multiple modules can be loaded from a single dll",TimeSpan.FromSeconds(5)) {}

        public override void Tick()
        {
            if (Core.Anima.Instance.SystemMail.CheckNumMessages(this) == 0)
            {
                Core.Anima.Instance.WriteLine($"No messages for: {this.Identifier}");
            }
            while (Core.Anima.Instance.SystemMail.CheckNumMessages(this) > 0)
            {
                Core.Anima.Instance.WriteLine(Core.Anima.Instance.SystemMail.GetMessage<int>(this));
            }
            
        }
    }
}
