﻿using Terminal.Gui;
using Terminal.Gui.TextValidateProviders;
using TerminalGuiDesigner.Operations;
using TerminalGuiDesigner.ToCode;
using Attribute = Terminal.Gui.Attribute;

namespace TerminalGuiDesigner.UI.Windows;


public class EditDialog : Window
{
    private List<Property> _collection;
    private ListView _list;

    public Design Design { get; }

    public EditDialog(Design design)
    {
        Design = design;
        _collection = Design.GetDesignableProperties()
            .OrderByDescending(p=>p is NameProperty)
            .ThenBy(p=>p.ToString())
            .ToList();

        // Don't let them rename the root view that would go badly
        // See `const string RootDesignName`
        if(design.IsRoot)
        {
            _collection = _collection.Where(p=>p is not NameProperty).ToList();
        }

        _list = new ListView(_collection)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2)
        };
        _list.KeyPress += List_KeyPress;

        var btnSet = new Button("Set")
        {
            X = 0,
            Y = Pos.Bottom(_list),
            IsDefault = true
        };

        btnSet.Clicked += () =>
        {
            SetProperty(false);
        };

        var btnClose = new Button("Close")
        {
            X = Pos.Right(btnSet),
            Y = Pos.Bottom(_list)
        };
        btnClose.Clicked += () => Application.RequestStop();

        Add(_list);
        Add(btnSet);
        Add(btnClose);
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        if(keyEvent.Key == Key.Enter && _list.HasFocus)
        {
            SetProperty(false);
            return true;
        }    

        return base.ProcessHotKey(keyEvent);
    }

    private void SetProperty(bool setNull)
    {
        if (_list.SelectedItem != -1)
        {
            try
            {
                var p = _collection[_list.SelectedItem];
                var oldValue = p.GetValue();

                if (setNull)
                {
                    // user wants to set this property to null/default
                    OperationManager.Instance.Do(
                        new SetPropertyOperation(Design, p, oldValue, null)
                    );
                }
                else
                {
                    if(!SetPropertyToNewValue(Design, p, oldValue))
                    {
                        // user cancelled editing the value
                        return;
                    }
                }

                var oldSelected = _list.SelectedItem;
                _list.SetSource(_collection = _collection.ToList());
                _list.SelectedItem = oldSelected;
                _list.EnsureSelectedItemVisible();

            }
            catch (Exception e)
            {
                ExceptionViewer.ShowException("Failed to set Property", e);
            }
        }
    }

    public static bool SetPropertyToNewValue(Design design, Property p, object? oldValue)
    {
        // user wants to give us a new value for this property
        if (GetNewValue(design, p, p.GetValue(), out object? newValue))
        {
            OperationManager.Instance.Do(
                new SetPropertyOperation(design, p, oldValue, newValue)
            );

            return true;
        }

        return false;
    }

    public static bool GetNewValue(Design design, Property property, object? oldValue, out object? newValue)
    {
        if(property.PropertyInfo.PropertyType == typeof(ColorScheme))
        {
            return GetNewColorSchemeValue(design, property, out newValue);            
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(Attribute) ||
            property.PropertyInfo.PropertyType == typeof(Attribute?))
        {
            // if its an Attribute or nullableAttribute
            var picker = new ColorPicker((Attribute?)property.GetValue());
            Application.Run(picker);

            if (!picker.Cancelled)
            {
                newValue = picker.Result;
                return true;
            }
            else
            {
                // user cancelled designing the Color
                newValue = null;
                return false;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(ITextValidateProvider))
        {
            string? oldPattern = oldValue is TextRegexProvider r ? (string?)r.Pattern.ToPrimitive() : null;
            if(Modals.GetString("New Validation Pattern","Regex Pattern",oldPattern,out var newPattern))
            {

                newValue = string.IsNullOrWhiteSpace(newPattern) ? null : new TextRegexProvider(newPattern);
                return true;
            }

            // user cancelled entering a pattern
            newValue = null;
            return false;

        }
        else
        // user is editing a Pos
        if (property.PropertyInfo.PropertyType == typeof(Pos))
        {
            var designer = new PosEditor(design, property);

            Application.Run(designer);


            if (!designer.Cancelled)
            {
                newValue = designer.Result;
                return true;
            }
            else
            {
                // user cancelled designing the Pos
                newValue = null;
                return false;
            }
        }
        else
        // user is editing a PointF
        if (property.PropertyInfo.PropertyType == typeof(PointF))
        {
            var oldPointF = (PointF)(oldValue ?? throw new Exception($"Property {property.PropertyInfo.Name} is of Type PointF but it's current value is null"));
            var designer = new PointEditor(oldPointF.X,oldPointF.Y);

            Application.Run(designer);


            if (!designer.Cancelled)
            {
                newValue = new PointF(designer.ResultX,designer.ResultY);
                return true;
            }
            else
            {
                // user cancelled designing the Pos
                newValue = null;
                return false;
            }
        }
        else
        // user is editing a Dim
        if (property.PropertyInfo.PropertyType == typeof(Dim))
        {
            var designer = new DimEditor(design, property);
            Application.Run(designer);

            if (!designer.Cancelled)
            {
                newValue = designer.Result;
                return true;
            }
            else
            {
                // user cancelled designing the Dim
                newValue = null;
                return false;
            }
                
        }
        else
        if (property.PropertyInfo.PropertyType == typeof(bool))
        {
            int answer = MessageBox.Query(property.PropertyInfo.Name, $"New value for {property.PropertyInfo.PropertyType}", "Yes", "No");

            newValue = answer ==0 ? true : false;
            return answer != -1;
        }
        else
        if(property.PropertyInfo.PropertyType.IsArray)
        {
            if(Modals.GetArray(property.PropertyInfo.Name, "New Array Value",
                property.PropertyInfo.PropertyType.GetElementType() ?? throw new Exception("Property was an Array but GetElementType returned null"),
                 (Array?)oldValue, out Array? resultArray))
            {
                newValue = resultArray;
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(IListDataSource))
        {
            // TODO : Make this work with non strings e.g. 
            // if user types a bunch of numbers in or dates
            var oldValueAsArrayOfStrings = oldValue == null ?
                    new string[0] :
                    ((IListDataSource)oldValue).ToList()
                                               .Cast<object>()
                                               .Select(o=>o?.ToString())
                                               .ToArray();

            if(Modals.GetArray(property.PropertyInfo.Name, "New List Value",typeof(string),
                 oldValueAsArrayOfStrings ?? new string[0], out Array? resultArray))
            {
                newValue = resultArray;
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType.IsEnum)
        {
            if(Modals.GetEnum(property.PropertyInfo.Name, "New Enum Value", property.PropertyInfo.PropertyType, out var resultEnum))
            {
                newValue = resultEnum;
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(int) 
            || property.PropertyInfo.PropertyType == typeof(int?)
            || property.PropertyInfo.PropertyType == typeof(uint) 
            || property.PropertyInfo.PropertyType == typeof(uint?))
        {
            // deals with null, int and uint
            var v = oldValue == null ? null : (int?)Convert.ToInt32(oldValue);

            if(Modals.GetInt(property.PropertyInfo.Name, "New Int Value", v, out var resultInt))
            {
                // change back to uint/int/null
                newValue = resultInt == null ? null : Convert.ChangeType(resultInt,property.PropertyInfo.PropertyType);
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(float) 
            || property.PropertyInfo.PropertyType == typeof(float?))
        {
            if(Modals.GetFloat(property.PropertyInfo.Name, "New Float Value", (float?)oldValue, out var resultInt))
            {
                newValue = resultInt;
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(char?)
            || property.PropertyInfo.PropertyType == typeof(char) 
            )
        {
            if(Modals.GetChar(property.PropertyInfo.Name, "New Single Character", oldValue is null ? null : (char?)oldValue.ToPrimitive() ?? null, out var resultChar))
            {
                newValue = resultChar;
                return true;
            }
        }
        else
        if(property.PropertyInfo.PropertyType == typeof(Rune) 
            || property.PropertyInfo.PropertyType == typeof(Rune?)
            )
        {
            if(Modals.GetChar(property.PropertyInfo.Name, "New Single Character", oldValue is null ? null : (char?)oldValue.ToPrimitive() ?? null, out var resultChar))
            {
                newValue = resultChar == null ? null : (Rune)resultChar;
                return true;
            }
        }
        else
        if (Modals.GetString(property.PropertyInfo.Name, "New String Value", oldValue?.ToString(), out var result,AllowMultiLine(property)))
        {
            newValue = result;
            return true;
        }

        newValue = null;
        return false;
    }

    private static bool GetNewColorSchemeValue(Design design, Property property, out object? newValue)
    {
        const string custom = "Custom...";
        List<object> offer = new();

        var defaults = new DefaultColorSchemes();
        var schemes = ColorSchemeManager.Instance.Schemes.ToList();

        offer.AddRange(schemes);

        foreach (var d in defaults.GetDefaultSchemes())
        {
            // user is already explicitly using this default and may even have modified it
            if (offer.OfType<NamedColorScheme>().Any(s => s.Name.Equals(d.Name)))
                continue;

            offer.Add(d);
        }

        // add the option to jump to custom colors
        offer.Add(custom);

        if (Modals.Get("Color Scheme", "Ok", offer.ToArray(), out var selected))
        {
            // if user clicked "Custom..."
            if (selected is string s && string.Equals(s, custom))
            {
                // show the custom colors dialog
                var colorSchemesUI = new ColorSchemesUI(design);
                Application.Run(colorSchemesUI);
                newValue = null;
                return false;
            }
            if(selected is NamedColorScheme ns)
            {
                newValue = ns.Scheme;
                
                // if it was a default one, tell ColorSchemeManager we are now using it
                if(!schemes.Contains(ns))
                {
                    ColorSchemeManager.Instance.AddOrUpdateScheme(ns.Name, ns.Scheme);
                }

                return true;
            }

            newValue = null;
            return false;
        }
        else
        {
            // user cancelled selecting scheme
            newValue = null;
            return false;
        }
    }

    private static bool AllowMultiLine(Property property)
    {
        // for the text editor control let them put multiple lines in
        if(property.PropertyInfo.Name.Equals("Text") && property.Design.View is TextView tv && tv.Multiline)
        {
            return true;
        }

        return false;
    }

    private void List_KeyPress(KeyEventEventArgs obj)
    {
        // TODO: Should really be using the _keyMap here
        if (obj.KeyEvent.Key == Key.DeleteChar)
        {
            int rly = MessageBox.Query("Clear", "Clear Property Value?", "Yes", "Cancel");
            obj.Handled = true;

            if (rly == 0)
            {
                SetProperty(true);
            }
        }
    }
}
