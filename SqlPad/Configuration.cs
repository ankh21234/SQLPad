﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by xsd, Version=4.0.30319.33440.
// 
namespace SqlPad {
    using System.Xml.Serialization;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://husqvik.com/SqlPad/2014/02")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace="http://husqvik.com/SqlPad/2014/02", IsNullable=false)]
    public partial class Configuration {
        
        private ConfigurationDataModel dataModelField;
        
        private ConfigurationResultGrid resultGridField;
        
        private ConfigurationEditor editorField;
        
        /// <remarks/>
        public ConfigurationDataModel DataModel {
            get {
                return this.dataModelField;
            }
            set {
                this.dataModelField = value;
            }
        }
        
        /// <remarks/>
        public ConfigurationResultGrid ResultGrid {
            get {
                return this.resultGridField;
            }
            set {
                this.resultGridField = value;
            }
        }
        
        /// <remarks/>
        public ConfigurationEditor Editor {
            get {
                return this.editorField;
            }
            set {
                this.editorField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://husqvik.com/SqlPad/2014/02")]
    public partial class ConfigurationDataModel {
        
        private uint dataModelRefreshPeriodField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public uint DataModelRefreshPeriod {
            get {
                return this.dataModelRefreshPeriodField;
            }
            set {
                this.dataModelRefreshPeriodField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://husqvik.com/SqlPad/2014/02")]
    public partial class ConfigurationResultGrid {
        
        private string dateFormatField;
        
        private string nullPlaceholderField;
        
        private int fetchRowsBatchSizeField;
        
        private bool fetchRowsBatchSizeFieldSpecified;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string DateFormat {
            get {
                return this.dateFormatField;
            }
            set {
                this.dateFormatField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string NullPlaceholder {
            get {
                return this.nullPlaceholderField;
            }
            set {
                this.nullPlaceholderField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int FetchRowsBatchSize {
            get {
                return this.fetchRowsBatchSizeField;
            }
            set {
                this.fetchRowsBatchSizeField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool FetchRowsBatchSizeSpecified {
            get {
                return this.fetchRowsBatchSizeFieldSpecified;
            }
            set {
                this.fetchRowsBatchSizeFieldSpecified = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://husqvik.com/SqlPad/2014/02")]
    public partial class ConfigurationEditor {
        
        private int indentationSizeField;
        
        private bool indentationSizeFieldSpecified;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int IndentationSize {
            get {
                return this.indentationSizeField;
            }
            set {
                this.indentationSizeField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool IndentationSizeSpecified {
            get {
                return this.indentationSizeFieldSpecified;
            }
            set {
                this.indentationSizeFieldSpecified = value;
            }
        }
    }
}
