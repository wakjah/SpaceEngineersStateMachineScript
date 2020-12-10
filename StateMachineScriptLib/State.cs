using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameScript
{
    partial class Program
    {
        abstract class State<ContextImpl> where ContextImpl : Context<ContextImpl>
        {
            public abstract void update(ContextImpl context);
            public virtual void enter(ContextImpl context) { }
            public virtual void leave(ContextImpl context) { }
        }
    }
}