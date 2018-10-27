﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hacknet;

namespace Pathfinder.Actions.SerializableCondition
{
    public abstract class Interface : Hacknet.SerializableCondition
    {
        public abstract bool Check(OS os);

        public override bool Check(object os_obj)
        {
            return Check((Hacknet.OS)os_obj);
        }
    }
}
