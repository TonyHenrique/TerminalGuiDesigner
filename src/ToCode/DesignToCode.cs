﻿using System.CodeDom;
using Terminal.Gui;

namespace TerminalGuiDesigner.ToCode;

internal class DesignToCode : ToCodeBase
{
    public DesignToCode(Design design)
    {
        Design = design;
    }

    public Design Design { get; }

    internal void ToCode(CodeDomArgs args, CodeExpression parentView)
    {
        AddFieldToClass(args, Design);
        var constructorCall = GetConstructorCall($"this.{Design.FieldName}",Design.View.GetType());
        args.InitMethod.Statements.Insert(0, constructorCall);

        foreach (var prop in Design.GetDesignableProperties())
        {
            prop.ToCode(args);
        }


        // if the current component is a TableView we should persist the table too
        if (Design.View is TableView tv && tv.Table != null)
        {
            var designTable = new DataTableToCode(Design, tv.Table);
            designTable.ToCode(args);
        }

        if(Design.View is MenuBar mb)
        {
            var designItems = new MenuBarItemsToCode(Design,mb);
            designItems.ToCode(args);
        }

        if(Design.View is TabView tabView)
        {
            foreach(var tab in tabView.Tabs)
            {
                var designTab = new TabToCode(Design,tab);
                designTab.ToCode(args);
            }

            // add call to ApplyStyleChanges();
            AddMethodCall(args,        
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), Design.FieldName),
                nameof(TabView.ApplyStyleChanges));
        }

        // call this.Add(someView)
        AddAddToViewStatement(args, Design, parentView);
    }
}
