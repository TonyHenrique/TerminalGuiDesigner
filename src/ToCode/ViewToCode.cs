﻿using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using Terminal.Gui;
using TerminalGuiDesigner.FromCode;

namespace TerminalGuiDesigner.ToCode;

/// <summary>
/// Converts a <see cref="View"/> in memory into code in a '.Designer.cs' class file
/// </summary>
public class ViewToCode
{
    /// <summary>
    /// Creates a new class file and accompanying '.Designer.cs' file based on
    /// <see cref="Window"/>
    /// </summary>
    /// <param name="csFilePath"></param>
    /// <param name="namespaceName"></param>
    /// <param name="designerFile">Designer.cs file that will be created along side the <paramref name="csFilePath"/></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public Design GenerateNewWindow(FileInfo csFilePath, string namespaceName, out SourceCodeFile sourceFile)
    {
        if (csFilePath.Name.EndsWith(SourceCodeFile.ExpectedExtension))
        {
            throw new ArgumentException($@"{nameof(csFilePath)} should be a class file not the designer file e.g. c:\MyProj\MyWindow1.cs");
        }


        var className = Path.GetFileNameWithoutExtension(csFilePath.Name);
        sourceFile = new SourceCodeFile(csFilePath);

        var csharpCode = GetGenerateNewWindowCode(className, namespaceName);
        File.WriteAllText(sourceFile.CsFile.FullName, csharpCode);

        var w = new Window();
        var lbl = new Label("Hello World");
        lbl.Data = "label1"; // field name in the class
        w.Add(lbl);

        var design = new Design(sourceFile, "root", w);
        design.CreateSubControlDesigns();

        GenerateDesignerCs(w, sourceFile);

        return design;
    }

    /// <summary>
    /// Returns the code that would be added to the MyWindow.cs file of a new window
    /// so that it is ready for use with the MyWindow.Designer.cs file (in which
    /// we will put all our design time gubbins).
    /// </summary>
    /// <param name="className"></param>
    /// <param name="namespaceName"></param>
    /// <returns></returns>
    public static string GetGenerateNewWindowCode(string className, string namespaceName)
    {
        string indent = "    ";

        var ns = new CodeNamespace(namespaceName);
        ns.Imports.Add(new CodeNamespaceImport("Terminal.Gui"));

        CodeCompileUnit compileUnit = new CodeCompileUnit();
        compileUnit.Namespaces.Add(ns);

        CodeTypeDeclaration class1 = new CodeTypeDeclaration(className);
        class1.IsPartial = true;
        class1.BaseTypes.Add(new CodeTypeReference("Window")); //TODO: let user create things that aren't windows

        ns.Types.Add(class1);

        var constructor = new CodeConstructor();
        constructor.Attributes = MemberAttributes.Public;
        constructor.Statements.Add(new CodeSnippetStatement($"{indent}{indent}{indent}{SourceCodeFile.InitializeComponentMethodName}();"));

        class1.Members.Add(constructor);

        CSharpCodeProvider provider = new CSharpCodeProvider();

        using (var sw = new StringWriter())
        {
            IndentedTextWriter tw = new IndentedTextWriter(sw, indent);

            // Generate source code using the code provider.
            provider.GenerateCodeFromCompileUnit(compileUnit, tw,
                new CodeGeneratorOptions());

            tw.Close();

            return sw.ToString();
        }
    }

    public void GenerateDesignerCs(View forView, SourceCodeFile file)
    {
        var rosylyn = new CodeToView(file);

        var ns = new CodeNamespace(rosylyn.Namespace);
        ns.Imports.Add(new CodeNamespaceImport("System"));
        ns.Imports.Add(new CodeNamespaceImport("Terminal.Gui"));


        CodeCompileUnit compileUnit = new CodeCompileUnit();
        compileUnit.Namespaces.Add(ns);

        CodeTypeDeclaration class1 = new CodeTypeDeclaration(rosylyn.ClassName);
        class1.IsPartial = true;

        var initMethod = new CodeMemberMethod();
        initMethod.Name = SourceCodeFile.InitializeComponentMethodName;

        AddSubViewsToDesignerCs(forView, new CodeDomArgs(class1, initMethod));

        class1.Members.Add(initMethod);
        ns.Types.Add(class1);

        CSharpCodeProvider provider = new CSharpCodeProvider();

        using (var sw = new StringWriter())
        {
            IndentedTextWriter tw = new IndentedTextWriter(sw, "    ");

            // Generate source code using the code provider.
            provider.GenerateCodeFromCompileUnit(compileUnit, tw,
                new CodeGeneratorOptions());

            tw.Close();

            File.WriteAllText(file.DesignerFile.FullName, sw.ToString());
        }
    }

    private void AddSubViewsToDesignerCs(View forView, CodeDomArgs args)
    {
        // TODO: we should detect RelativeTo etc here meaning one view depends
        // on anothers position and therefore the dependant view should be output
        // after

        // order the controls top left to lower right so that tab order is good
        foreach (var sub in forView.Subviews.OrderBy(v=>v.Frame.Y).ThenBy(v=>v.Frame.X))
        {
            // If the sub child has a Design (and is not an public part of another control,
            // For example Contentview subview of Window
            if (sub.Data is Design d)
            {
                // The user is designing this view so it needs to be persisted
                d.ToCode(args);
            }

            // now recurse down the view hierarchy
            AddSubViewsToDesignerCs(sub, args);
        }
    }
}