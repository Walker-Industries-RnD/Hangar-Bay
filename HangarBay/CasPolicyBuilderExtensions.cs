using DouglasDwyer.CasCore;
using System.Reflection;

public static class CasPolicyBuilderExtensions
{
    public static CasPolicyBuilder ApplyModTypeConfig(this CasPolicyBuilder builder,
        Dictionary<string, List<string>>? allowedConfig)
    {
        if (allowedConfig == null || allowedConfig.Count == 0)
        {

            return builder.WithDefaultSandbox();
        }

        foreach (var (typeName, members) in allowedConfig)
        {
            Type? type = Type.GetType(typeName, throwOnError: false);
            if (type == null)
            {
                Console.WriteLine($"Warning: Sandbox config references unknown type '{typeName}' — skipping.");
                continue;
            }

            var binding = new TypeBinding(type, Accessibility.Public);

            if (members == null || members.Count == 0 || members.Contains("*"))
            {
                builder.Allow(binding);
            }
            else
            {
                var partial = new TypeBinding(type, Accessibility.None);

                foreach (var memberName in members)
                {
                    if (memberName == ".ctor" || memberName.StartsWith("ctor("))
                    {

                        partial = partial.WithConstructor(Type.EmptyTypes, Accessibility.Public);
                    }
                    else if (memberName.StartsWith("op_"))
                    {
                        Console.WriteLine($"Operator '{memberName}' in config — not auto-supported yet.");
                        continue;
                    }
                    else
                    {
                        // Try method
                        partial = partial.WithMethod(memberName, Accessibility.Public);

                        // Try field
                        partial = partial.WithField(memberName, Accessibility.Public);

                        // Try property (add getter/setter methods if found)
                        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        if (property != null)
                        {
                            if (property.GetGetMethod(true) != null)
                                partial = partial.WithMethod(property.GetGetMethod(true)!.Name, Accessibility.Public);

                            if (property.GetSetMethod(true) != null)
                                partial = partial.WithMethod(property.GetSetMethod(true)!.Name, Accessibility.Public);
                        }
                    }
                }

                builder.Allow(partial);
            }
        }

        return builder;
    }
}