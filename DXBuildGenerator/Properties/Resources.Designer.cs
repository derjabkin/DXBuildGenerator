﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DXBuildGenerator.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("DXBuildGenerator.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Directory &apos;{0}&apos; not found..
        /// </summary>
        internal static string DirectoryNotFound {
            get {
                return ResourceManager.GetString("DirectoryNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Either -x or -s and -r options should be specified.\r\n.
        /// </summary>
        internal static string NoPathSpecifiedMessage {
            get {
                return ResourceManager.GetString("NoPathSpecifiedMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to WARNING: The generated script will replace original DevExpress Assemblies!.
        /// </summary>
        internal static string OriginalFilesReplacementWarning {
            get {
                return ResourceManager.GetString("OriginalFilesReplacementWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -r option must be specified..
        /// </summary>
        internal static string ReferencePathNotSpecified {
            get {
                return ResourceManager.GetString("ReferencePathNotSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -s option must be specified..
        /// </summary>
        internal static string SourceCodeDirNotSpecified {
            get {
                return ResourceManager.GetString("SourceCodeDirNotSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Use --help option for usage.
        /// </summary>
        internal static string UseHelpOptionForUsage {
            get {
                return ResourceManager.GetString("UseHelpOptionForUsage", resourceCulture);
            }
        }
    }
}
