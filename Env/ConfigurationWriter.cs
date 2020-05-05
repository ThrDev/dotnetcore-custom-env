using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Env.Exceptions;

namespace Env
{
    public class ConfigurationWriter
    {
        public static void WriteToFile<T>(T src, string filePath, bool generateComments = false)
        {
            var configurationWriter = new ConfigurationWriter();
            configurationWriter.Write(src, filePath, generateComments);
        }

        public void Write<T>(T src, string filePath, bool generateComments = false)
        {
            var file = File.CreateText(filePath);
            WriteProperties(src, typeof(T), null, new Stack<Type>(), file, generateComments);
            file.Close();
        }
        
        private void WriteProperties<T>(T instance, Type type, string prefix, Stack<Type> recursive, StreamWriter fileStream, bool generateComments)
        {
            Push(recursive, type);

            var subItems = new Dictionary<string, PropertyInfo>();
            
            foreach (PropertyInfo prop in type.GetProperties())
            {
                if (prop.MemberType != MemberTypes.Property)
                {
                    continue;
                }

                var itemName = prop.Name;

                var configItemAttr = prop.GetCustomAttributes(true).FirstOrDefault(a => a.GetType() == typeof(ConfigItem));
                var ignoreConfigItemAttr = prop.GetCustomAttributes(true).FirstOrDefault(a => a.GetType() == typeof(IgnoreConfigItem));
                if (ignoreConfigItemAttr is IgnoreConfigItem)
                {
                    continue;
                }
                
                if (configItemAttr is ConfigItem cAttr)
                {
                    itemName = cAttr.Name;
                }

                var isNullable = Nullable.GetUnderlyingType(prop.PropertyType);
                
                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || isNullable != null)
                {
                    var value = prop.GetValue(instance);

                    if (value != null)
                    {
                        // try to cast env to that type.
                        var convertedVal = Convert.ChangeType(value, typeof(string));

                        fileStream.WriteLine($"{(prefix != null ? $"{prefix + "_" ?? ""}{itemName}" : itemName)} = {convertedVal}");
                    }
                    else
                    {
                        if (generateComments)
                        {
                            fileStream.WriteLine($"# {(prefix != null ? $"{prefix + "_" ?? ""}{itemName}" : itemName)} = null");
                        }
                    }
                }
                else
                {
                    subItems.Add(itemName, prop);
                }
                
            }

            foreach (var item in subItems)
            {
                var itemName = item.Key;
                var subInstance = item.Value.GetValue(instance);
                
                if (generateComments)
                {
                    fileStream.WriteLine();
                    fileStream.Write($"#\n# {(prefix != null ? prefix + "_" : "")}{itemName}\n#\n");
                }
                
                WriteProperties(subInstance, item.Value.PropertyType, $"{(prefix != null ? prefix + "_" : "")}{itemName}", recursive, fileStream, generateComments);
            }

            recursive.Pop();
        }
        
        private void Push(Stack<Type> stack, Type item)
        {
            if (stack.Contains(item))
            {
                throw new RecursiveClassException($"Class instantiation loop detected... Type already initialized: {item.ToString()}");
            }

            stack.Push(item);
        }
    }
}