CAUTION: This is a pre-alpha version. The programm modifies .cs and .csproj files. Please make backup copies of the originals!


Requirements:

- Silverlight 5.0

Following assemblies are required to be installed in the GAC:

Microsoft.VisualStudio.Shell, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL
Microsoft.VisualStudio.Shell.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL
Microsoft.VisualStudio.Shell.Design, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL



Usage: DXBuildGenerator -s <source directory> -r <references directory>
Or: DXBuildGenerator -x <devexpress root directory>

Example DXGenerator -x "c:\Program Files (x86)\DevExpress\DXperience 12.2"

  -x          Path to the DevExpress installation folder.If this option is
              specified, Source code directory,  references directory and the
              output path are determinated automatically.

  --op        Output path for the compiled assemblies. If the value is not
              specified, the property value from the template will be used.

  -t          (Default: Template.proj) Template file name

  -o          (Default: build.proj) Output file name

  -s          Source code directory

  -r          Reference files root directory

  --nosl      Skip silverlight projects

  --notest    Skip test projects

  --nomvc     Skip ASP.NET MVC projects

  --help      Display this help screen.
