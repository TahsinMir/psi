﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Helper class for type resolution.
    /// </summary>
    public static class TypeResolutionHelper
    {
        /// <summary>
        /// Gets a type by its type name.  This method will only return types from loaded
        /// assemblies, i.e. assemblies explicitly referenced or loaded by this application.
        /// </summary>
        /// <param name="typeName">The name of the type to retrieve.</param>
        /// <returns>The requested type, or null if the type was not found.</returns>
        public static Type GetVerifiedType(string typeName)
        {
            return Type.GetType(typeName, AssemblyResolver, null);
        }

        /// <summary>
        /// Removes the assembly name from an assembly-qualified type name, returning the fully
        /// qualified name of the type, including its namespace but not the assembly name.
        /// </summary>
        /// <param name="assemblyQualifiedName">A string representing the assembly-qualified name of a type.</param>
        /// <returns>The fully qualified name of the type, including its namespace but not the assembly name.</returns>
        internal static string RemoveAssemblyName(string assemblyQualifiedName)
        {
            string typeName = assemblyQualifiedName;

            // strip out all assembly names (including in nested type parameters)
            typeName = Regex.Replace(typeName, @",\s[^,\[\]\*]+", string.Empty);

            return typeName;
        }

        private static Assembly AssemblyResolver(AssemblyName assemblyName)
        {
            // Get the list of currently loaded assemblies
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Attempt to match by full name first
            var assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().FullName == assemblyName.FullName);
            if (assembly != null)
            {
                return assembly;
            }

            // Otherwise try to match by simple name without version, culture or key
            assembly = loadedAssemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), assemblyName));
            if (assembly != null)
            {
                return assembly;
            }

            return null;
        }
    }
}
