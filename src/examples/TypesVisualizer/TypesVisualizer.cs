﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenSoftware.DgmlTools.Analyses;
using OpenSoftware.DgmlTools.Builders;
using OpenSoftware.DgmlTools.Model;

namespace OpenSoftware.DgmlTools
{
    public static class TypesVisualizer
    {
        private const string AbstractClassType = "Abstract";
        private const string InterfaceType = "Interface";
        private const string AssociationRelation = "Association";
        private const string InheritanceRelation = "Inheritance";
        private const string ClassType = "Class";

        /// <summary>
        /// Creates a DGML graph from a collection of types. The graph contains 
        /// nodes for interfaces (denoted as ovals) and classes (denoted as boxes), dashed edges for inheritance relations, 
        /// normal edges for asociations. Abstract classes are denoted as dashed boxes.
        /// </summary>
        /// <param name="types"></param>
        /// <returns></returns>
        public static DirectedGraph Types2Dgml(IEnumerable<Type> types)
        {
            var typesCollection = types.ToArray();
            var builder = new DgmlBuilder(new HubNodeAnalysis())
            {
                NodeBuilders = new[]
                {
                    new NodeBuilder<Type>(Class2Node, x => x.IsClass),
                    new NodeBuilder<Type>(Interface2Node, x => x.IsInterface)
                },
                LinkBuilders = new[]
                {
                    new LinksBuilder<Type>(x => Property2Links(x, typesCollection)),
                    new LinksBuilder<Type>(x => Field2Links(x, typesCollection)),
                    new LinksBuilder<Type>(x => TypeInheritace2Links(x, typesCollection)),
                    new LinksBuilder<Type>(x => GenericType2Links(x, typesCollection)),
                    new LinksBuilder<Type>(x => ConstructorInjection2Links(x, typesCollection)),
                },
                CategoryBuilders = new[] {new CategoryBuilder<Type>(Type2Category)},
                StyleBuilders = new StyleBuilder[]
                {
                    new StyleBuilder<Node>(InterfaceStyle, x => x.HasCategory(InterfaceType)),
                    new StyleBuilder<Node>(AbstractStyle, x => x.HasCategory(AbstractClassType)),
                    new StyleBuilder<Link>(AssociationStyle, x => x.HasCategory(AssociationRelation)),
                    new StyleBuilder<Link>(InheritanceStyle, x => x.HasCategory(InheritanceRelation))
                }
            };
            return builder.Build(typesCollection);
        }


        private static Node Class2Node(Type type)
        {
            var node = new Node
            {
                Category = ClassType,
                CategoryRefs = new List<CategoryRef> {new CategoryRef {Ref = "a:" + GetAssemblyName(type)}},
                Id = MakeTypeId(type),
                Label = type.Name
            };
            if (type.IsAbstract)
            {
                node.CategoryRefs.Add(new CategoryRef {Ref = AbstractClassType});
            }
            return node;
        }

        public static Node Interface2Node(Type type)
        {
            return new Node
            {
                Category = InterfaceType,
                CategoryRefs = new List<CategoryRef> {new CategoryRef {Ref = "a:" + GetAssemblyName(type)}},
                Id = MakeTypeId(type),
                Label = type.Name
            };
        }


        private static IEnumerable<Link> Property2Links(Type type, Type[] types)
        {
            var properties = type.GetProperties()
                .Where(x => x.DeclaringType == type);

            foreach (var propertyInfo in properties)
            {
                var propertyType = propertyInfo.PropertyType;
                var propertyName = propertyInfo.Name;
                var link = MakeAssociation(type, propertyType, propertyName, types);
                if (link == null) continue;
                yield return link;
            }
        }

        private static IEnumerable<Link> Field2Links(Type type, Type[] types)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly |
                                        BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var fieldInfo in fields)
            {
                var fieldType = fieldInfo.FieldType;
                var fieldName = fieldInfo.Name;
                var link = MakeAssociation(type, fieldType, fieldName, types);
                if (link == null) continue;
                yield return link;
            }
        }

        private static IEnumerable<Link> TypeInheritace2Links(Type type, Type[] types)
        {
            var baseType = type.BaseType;

            if (baseType != null && HasType(types, baseType))
            {
                yield return MakeInheritanceLink(type, baseType);
            }
            foreach (var i in type.GetInterfaces().Where(x => HasType(types, x)))
            {
                yield return MakeInheritanceLink(type, i);
            }
        }

        private static IEnumerable<Link> GenericType2Links(Type type, Type[] types)
        {
            var baseType = type.BaseType;

            if (baseType != null && HasType(types, baseType) && baseType.IsGenericType)
            {
                foreach (var genericTypeArgument in baseType.GetGenericArguments())
                {
                    yield return MakeAssociation(type, genericTypeArgument, null, types);
                }
            }
            foreach (var i in type.GetInterfaces().Where(x => HasType(types, x)).Where(x => x.IsGenericType))
            {
                foreach (var genericTypeArgument in i.GetGenericArguments())
                {
                    yield return MakeAssociation(i, genericTypeArgument, null, types);
                }
            }
        }

        private static IEnumerable<Link> ConstructorInjection2Links(Type type, Type[] types)
        {
            foreach (var constructorInfo in type.GetConstructors())
            {
                foreach (var parameterInfo in constructorInfo.GetParameters())
                {
                    yield return MakeAssociation(type, parameterInfo.ParameterType, null, types);
                }
            }
        }


        private static Category Type2Category(Type type)
        {
            var assemblyName = GetAssemblyName(type);

            return new Category {Id = "a:" + assemblyName, Label = assemblyName};
        }


        private static Style InterfaceStyle(Node node)
        {
            return new Style
            {
                GroupLabel = InterfaceType,
                Setter = new List<Setter>
                {
                    new Setter {Property = "NodeRadius", Value = "16"}
                }
            };
        }

        private static Style AbstractStyle(Node node)
        {
            return new Style
            {
                GroupLabel = AbstractClassType,
                Setter = new List<Setter>
                {
                    new Setter {Property = "StrokeDashArray", Value = "2,2"},
                }
            };
        }

        private static Style AssociationStyle(Link link)
        {
            return new Style
            {
                GroupLabel = link.Category,
                Setter = new List<Setter>
                {
                    new Setter {Property = "Stroke", Value = "LightBlue"}
                }
            };
        }

        private static Style InheritanceStyle(Link link)
        {
            return new Style
            {
                GroupLabel = link.Category,
                Setter = new List<Setter>
                {
                    new Setter {Property = "StrokeDashArray", Value = "2,2"},
                    new Setter {Property = "Stroke", Value = "Green"}
                }
            };
        }

        private static Link MakeInheritanceLink(Type type, Type baseType)
        {
            return new Link
            {
                Source = MakeTypeId(type),
                Target = MakeTypeId(baseType),
                Category = InheritanceRelation
            };
        }

        private static Link MakeAssociation(Type from, Type to, string name, IEnumerable<Type> types)
        {
            if (to.IsGenericType)
            {
                to = to.GetGenericArguments()[0];
            }

            if (HasType(types, to) == false) return null;
            return new Link
            {
                Source = MakeTypeId(from),
                Target = MakeTypeId(to),
                Label = name,
                Category = AssociationRelation
            };
        }

        private static string MakeTypeId(Type type)
        {
            return $"{type.Namespace}.{type.Name}";
        }

        private static bool HasType(IEnumerable<Type> types, Type type)
        {
            return types.Any(t => t.Namespace == type.Namespace && t.Name == type.Name);
        }

        private static string GetAssemblyName(Type type)
        {
            var assemblyName = type.Assembly.GetName();
            return assemblyName.Name;
        }
    }
}