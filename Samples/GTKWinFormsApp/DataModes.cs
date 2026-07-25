using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GTKWinFormsApp
{
    [DataContract]
    public class TestDataMode
    {
        [DataMember]
        public string name { get; set; } = string.Empty;
        [DataMember]
        public string treeID { get; set; } = string.Empty;
        [DataMember]
        public string parent { get; set; } = string.Empty;
        [DataMember]
        public string treeName { get; set; } = string.Empty;
    }
    [DataContract]
    public class TestEntity
    {
        [DataMember]
        public int ID { get; set; }
        public string title = string.Empty;
        [DataMember]
        public string Title { get { return title; } set { title = value; } }
        [DataMember]
        public string Info { get; set; } = string.Empty;
        [DataMember]
        public bool State { get; set; }
        [DataMember]
        public string CreateDate { get; set; } = string.Empty;
        [DataMember]
        public string Operate { get; set; } = string.Empty;
        [DataMember]
        public string PIC1 { get; set; } = string.Empty;
        [DataMember]
        public Image? PIC { get; set; }
    }

}
