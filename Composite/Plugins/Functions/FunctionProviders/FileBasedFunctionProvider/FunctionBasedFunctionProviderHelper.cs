﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Composite.Core;
using Composite.Core.Extensions;
using Composite.Functions;

namespace Composite.Plugins.Functions.FunctionProviders.FileBasedFunctionProvider
{
    /// <summary>
    /// Helper class for developing implementations of FileBasedFunctionProvider
    /// </summary>
    public static class FunctionBasedFunctionProviderHelper
    {
        private static readonly string LogTitle = typeof (FunctionBasedFunctionProviderHelper).FullName;

        /// <summary>
        /// Gets the function description from the <see cref="FunctionAttribute" />.
        /// </summary>
        /// <param name="functionName">Name of the function.</param>
        /// <param name="functionObject">The object that represents a function.</param>
        /// <returns></returns>
        public static string GetDescription(string functionName, object functionObject)
        {
            var attr = functionObject.GetType()
                                     .GetCustomAttributes(typeof(FunctionAttribute), false)
                                     .Cast<FunctionAttribute>()
                                     .FirstOrDefault();
            if (attr != null)
            {
                return attr.Description;
            }

            return String.Format("A {0} function", functionName);
        }

        /// <summary>
        /// Extracts the function paramteres from an object that represents a function.
        /// </summary>
        /// <param name="functionObject">The object that represents a function.</param>
        /// <param name="baseFunctionType">Type of the base function.</param>
        /// <returns></returns>
        public static IDictionary<string, FunctionParameter> GetParameters(object functionObject, Type baseFunctionType)
        {
            var dict = new Dictionary<string, FunctionParameter>();

            var type = functionObject.GetType();
            while (type != baseFunctionType && type != null)
            {
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.DeclaredOnly);
                foreach (var prop in properties)
                {
                    // Skipping overriden base properties
                    if (prop.GetAccessors()[0].GetBaseDefinition().DeclaringType == baseFunctionType) continue;

                    var propType = prop.PropertyType;
                    var name = prop.Name;
                    var att = prop.GetCustomAttributes(typeof(FunctionParameterAttribute), false).Cast<FunctionParameterAttribute>().FirstOrDefault();
                    WidgetFunctionProvider widgetProvider = null;

                    if (att != null && att.HasWidgetMarkup)
                    {
                        try
                        {
                            widgetProvider = att.GetWidgetFunctionProvider(type, prop);
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning(LogTitle, "Failed to get widget function provider for parameter property {0}"
                                                     .FormatWith(prop.Name));
                            Log.LogWarning(LogTitle, ex);
                        }
                    }

                    if (!dict.ContainsKey(name))
                    {
                        dict.Add(name, new FunctionParameter(name, propType, att, widgetProvider));
                    }
                }

                type = type.BaseType;
            }

            return dict;
        }
    }
}