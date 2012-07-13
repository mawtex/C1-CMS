﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Composite.Core.Linq;
using Composite.Data.DynamicTypes;


namespace Composite.Plugins.Data.DataProviders.XmlDataProvider
{
    /// <summary>
    /// This class contains information used by the XmlDataProvider 
    /// when handling CRUD operations.
    /// There exists one of these per data type. 
    /// This class contains one entry per data-scope/locale-scope that contains
    /// the filename and element name.
    /// </summary>
    [DebuggerDisplay("{DataTypeDescriptor}")]
    internal sealed class XmlDataTypeStore
    {
        private IXmlDataProviderHelper _helper = null;

        private readonly IEnumerable<XmlDataTypeStoreDataScope> _xmlDateTypeStoreDataScopes;


        public XmlDataTypeStore(DataTypeDescriptor dataTypeDescriptor, Type dataProviderHelperType, Type dataIdClassType, IEnumerable<XmlDataTypeStoreDataScope> xmlDateTypeStoreDataScopes, bool isGeneretedDataType)
        {
            if (dataProviderHelperType == null) throw new ArgumentNullException("dataProviderHelperType");
            if (dataIdClassType == null) throw new ArgumentNullException("dataIdClassType");

            DataTypeDescriptor =  dataTypeDescriptor;
            DataProviderHelperType = dataProviderHelperType;
            DataIdClassType = dataIdClassType;

            _xmlDateTypeStoreDataScopes = xmlDateTypeStoreDataScopes.Evaluate();

            IsGeneretedDataType = isGeneretedDataType;
        }


        public DataTypeDescriptor DataTypeDescriptor { get; private set; }

        /// <summary>
        /// This is a implementation of <see cref="IXmlDataProviderHelper"/> and <see cref="Composite.Plugins.Data.DataProviders.XmlDataProvider.CodeGeneration.DataProviderHelperBase"/>
        /// </summary>
        public Type DataProviderHelperType { get; private set; }

        public Type DataIdClassType { get; private set; }


        public bool IsGeneretedDataType { get; private set; }


        public bool HasDataScopeName(string dataScopeName)
        {
            return _xmlDateTypeStoreDataScopes.Any(f => f.DataScopeName == dataScopeName);
        }



        public XmlDataTypeStoreDataScope GetXmlDateTypeStoreDataScope(string dataScopeName, string cultureName)
        {
            XmlDataTypeStoreDataScope dateTypeStoreDataScope =
                _xmlDateTypeStoreDataScopes.SingleOrDefault(f => f.DataScopeName == dataScopeName && f.CultureName == cultureName);


            Verify.IsNotNull(dateTypeStoreDataScope, "No data store exist for data scope ({0},{1})", dataScopeName, cultureName);

            return dateTypeStoreDataScope;
        }


        internal IEnumerable<XmlDataTypeStoreDataScope> XmlDataTypeStoreDataScopes { get { return _xmlDateTypeStoreDataScopes; } }


        public IXmlDataProviderHelper Helper
        {
            get
            {
                if (_helper == null)
                {
                    if (_helper == null)
                    {
                        _helper = (IXmlDataProviderHelper)Activator.CreateInstance(this.DataProviderHelperType);
                    }
                }

                return _helper;
            }
        }
    }
}
