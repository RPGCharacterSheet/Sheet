﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoModels.Models
{
    public class Spell : MongoEntityBase<Spell>
    {
        protected Spell() : base()
        {
            // initialize a default Spell
        }
    }
}
