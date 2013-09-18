using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Joe.Web.Mvc
{
    public class ControllerDisplayAttribute : Attribute
    {
        public Boolean Hide { get; set; }
        public String Name { get; set; }

        public ControllerDisplayAttribute(String name)
            : this(name, false)
        {

        }

        public ControllerDisplayAttribute(Boolean hide)
            : this(null, hide)
        {

        }

        public ControllerDisplayAttribute(String name, Boolean hide)
        {
            Name = name;
            Hide = hide;
        }
    }
}