using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TypeLitePlus.TsModels;

namespace TypeLitePlus.KO
{
    public class TsKnockoutModelGenerator : TsGenerator
    {
        public const string KoClass = "ko-class";
        public const string KoInterface = "ko-interface";
        public const string Poco = "poco";

        public static IDictionary<TsModuleMember, string> Tags { get; } = new Dictionary<TsModuleMember, string>();

        public TsKnockoutModelGenerator(bool ignoreModuleNamespaces = false, string overrideModuleNamespaces = null)
        {
            if (!string.IsNullOrEmpty(overrideModuleNamespaces))
            {
                SetModuleNameFormatter(module => overrideModuleNamespaces);
            }
            else
            {
                if (ignoreModuleNamespaces)
                {
                    SetModuleNameFormatter(module => string.Empty);
                }
            }

            _typeFormatters.RegisterTypeFormatter<TsClass>((type, formatter) =>
            {
                var tsClass = ((TsClass)type);
                string className = tsClass.Name;

                if (!Tags.TryGetValue(tsClass, out string tag) || tag == KoInterface)
                {
                    className = "I" + className;
                }

                if (!tsClass.GenericArguments.Any())
                {
                    return className;
                }
                else
                {
                    return className + "<" + string.Join(", ", tsClass.GenericArguments.Select(a => a as TsCollection != null ? this.GetFullyQualifiedTypeName(a) + "[]" : this.GetFullyQualifiedTypeName(a))) + ">";
                }
            });

            this.RegisterTypeConvertor<Guid>(o => "string");
            this.RegisterTypeConvertor<TimeSpan>(o => "string");
        }

        /// <summary>
        /// Appends the appropriate class definition for the specified member
        /// </summary>
        /// <param name="classModel"></param>
        /// <param name="sb"></param>
        /// <param name="generatorOutput"></param>
        protected override void AppendClassDefinition(TsClass classModel, ScriptBuilder sb, TsGeneratorOutput generatorOutput)
        {
            Tags.TryGetValue(classModel, out string tag);

            sb.AppendLineIndented($"// {classModel.Name} ({tag})");
            switch (tag)
            {
                case KoClass:
                    AppendClassDefinition(classModel, sb, generatorOutput, true);
                    break;

                case KoInterface:
                    AppendKoInterfaceDefinition(classModel, sb, generatorOutput);
                    break;

                case Poco:
                    AppendClassDefinition(classModel, sb, generatorOutput, false);
                    break;

                default:
                    base.AppendClassDefinition(classModel, sb, generatorOutput);
                    break;
            }

            _generatedClasses.Add(classModel);
        }

        private void AppendClassDefinition(TsClass classModel, ScriptBuilder sb, TsGeneratorOutput generatorOutput, bool ko)
        {
            string typeName = this.GetTypeName(classModel);

            string visibility = this.GetTypeVisibility(classModel, typeName) ? "export " : "";
            sb.AppendIndented($"{visibility}class {typeName}");
            if (classModel.BaseType != null)
            {
                string baseTypeName = this.GetFullyQualifiedTypeName(classModel.BaseType);
                if (baseTypeName.StartsWith(classModel.Module.Name + ".", StringComparison.Ordinal))
                {
                    baseTypeName = baseTypeName.Substring(classModel.Module.Name.Length + 1);
                }

                sb.Append($" extends {baseTypeName}");
                sb.AppendLine(" {");
                using (sb.IncreaseIndentation())
                {
                    sb.AppendLineIndented("constructor() {");
                    using (sb.IncreaseIndentation())
                    {
                        sb.AppendLineIndented("super();");
                    }
                    sb.AppendLineIndented("}");
                }
            }
            else
            {
                sb.AppendLine(" {");
            }
            var members = new List<TsProperty>();
            if ((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
            {
                members.AddRange(classModel.Properties);
            }
            if ((generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
            {
                members.AddRange(classModel.Fields);
            }
            using (sb.IncreaseIndentation())
            {
                foreach (TsProperty property in members.Where(m => !m.IsIgnored))
                {
                    string propTypeName = this.GetPropertyType(property);
                    if (IsCollection(property))
                    {
                        if (propTypeName.EndsWith("[]", StringComparison.Ordinal))
                        {
                            propTypeName = propTypeName.Substring(0, propTypeName.Length - "[]".Length);
                        }
                        if (ko)
                        {
                            sb.AppendLineIndented($"{this.GetPropertyName(property)}: KnockoutObservableArray<{propTypeName}> = ko.observableArray([]);");
                        }
                        else
                        {
                            sb.AppendLineIndented($"{this.GetPropertyName(property)}: {propTypeName}[];");
                        }
                    }
                    else
                    {
                        if (ko)
                        {
                            sb.AppendLineIndented($"{this.GetPropertyName(property)}: KnockoutObservable<{propTypeName}> = ko.observable(null);");
                        }
                        else
                        {
                            sb.AppendLineIndented($"{this.GetPropertyName(property)}: {propTypeName};");
                        }
                    }
                }
            }

            sb.AppendLineIndented("}");
            _generatedClasses.Add(classModel);
        }

        private bool IsCollection(TsProperty property)
            => typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType.Type)
                && property.PropertyType.Type != typeof(string);

        private void AppendKoInterfaceDefinition(TsClass classModel, ScriptBuilder sb, TsGeneratorOutput generatorOutput)
        {
            string typeName = this.GetTypeName(classModel);
            string visibility = this.GetTypeVisibility(classModel, typeName) ? "export " : "";
            sb.AppendIndented($"{visibility}interface {typeName}");
            if (classModel.BaseType != null)
            {
                sb.Append($" extends {this.GetFullyQualifiedTypeName(classModel.BaseType)}");
            }
            sb.AppendLine(" {");
            var members = new List<TsProperty>();
            if ((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
                members.AddRange(classModel.Properties);
            if ((generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
                members.AddRange(classModel.Fields);
            using (sb.IncreaseIndentation())
            {
                foreach (TsProperty property in members)
                {
                    if (property.IsIgnored)
                        continue;
                    string propTypeName = this.GetPropertyType(property);
                    if (IsCollection(property))
                    {
                        if (propTypeName.Length > 2 && propTypeName.Substring(propTypeName.Length - 2) == "[]")
                            propTypeName = propTypeName.Substring(0, propTypeName.Length - 2);
                        propTypeName = "KnockoutObservableArray<" + propTypeName + ">";
                    }
                    else
                        propTypeName = "KnockoutObservable<" + propTypeName + ">";
                    sb.AppendLineIndented($"{this.GetPropertyName(property)}: {propTypeName};");
                }
            }
            sb.AppendLineIndented("}");
        }

        private static readonly FieldInfo _typeConvertersMember
            = typeof(TsGenerator).GetField("_typeConvertors", BindingFlags.Instance | BindingFlags.NonPublic);

        protected override void AppendModule(TsModule module, ScriptBuilder sb, TsGeneratorOutput generatorOutput)
        {
            // Use reflection to grab the internal property which was removed!
            var typeConverters = (TypeConvertorCollection)_typeConvertersMember.GetValue(this);

            var classes = module.Classes.Where(c => !typeConverters.IsConvertorRegistered(c.Type) && !c.IsIgnored)
                // .OrderBy(c => GetTypeName(c))			// Sorting breaks inheritance
                .ToList();
            var baseClasses = classes
                .Where(c => c.BaseType != null)
                .Select(c => c.BaseType.Type.FullName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            var enums = module.Enums.Where(e => !typeConverters.IsConvertorRegistered(e.Type) && !e.IsIgnored).OrderBy(e => GetTypeName(e)).ToList();
            if ((generatorOutput == TsGeneratorOutput.Enums && enums.Count == 0) ||
                (generatorOutput == TsGeneratorOutput.Properties && classes.Count == 0) ||
                (enums.Count == 0 && classes.Count == 0))
            {
                return;
            }

            if (generatorOutput == TsGeneratorOutput.Properties && !classes.Any(c => c.Fields.Any() || c.Properties.Any()))
            {
                return;
            }

            if (generatorOutput == TsGeneratorOutput.Constants && !classes.Any(c => c.Constants.Any()))
            {
                return;
            }

            string moduleName = GetModuleName(module);
            bool generateModuleHeader = moduleName != string.Empty;

            if (generateModuleHeader)
            {
                if (generatorOutput != TsGeneratorOutput.Enums &&
                    (generatorOutput & TsGeneratorOutput.Constants) != TsGeneratorOutput.Constants)
                {
                    sb.Append(Mode == TsGenerationModes.Definitions ? "declare " : " export ");
                }

                sb.AppendLine($"{(Mode == TsGenerationModes.Definitions ? "namespace" : "module")} {moduleName} {{");
            }

            using (sb.IncreaseIndentation())
            {
                if ((generatorOutput & TsGeneratorOutput.Enums) == TsGeneratorOutput.Enums)
                {
                    foreach (TsEnum enumModel in enums)
                    {
                        this.AppendEnumDefinition(enumModel, sb, generatorOutput);
                    }
                }

                if (((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
                    || (generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
                {
                    foreach (TsClass baseClassModel in classes.Where(c => baseClasses.Contains(c.Type.FullName)))
                    {
                        this.AppendClassDefinition(baseClassModel, sb, generatorOutput);
                    }
                }

                if (((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
                    || (generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
                {
                    foreach (TsClass classModel in classes.Where(c => !baseClasses.Contains(c.Type.FullName)))
                    {
                        this.AppendClassDefinition(classModel, sb, generatorOutput);
                    }
                }

                if ((generatorOutput & TsGeneratorOutput.Constants) == TsGeneratorOutput.Constants)
                {
                    foreach (TsClass classModel in classes)
                    {
                        if (classModel.IsIgnored)
                        {
                            continue;
                        }

                        this.AppendConstantModule(classModel, sb);
                    }
                }
            }
            if (generateModuleHeader)
            {
                sb.AppendLine("}");
            }
        }
    }
}