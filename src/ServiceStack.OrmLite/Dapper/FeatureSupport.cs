﻿using System;
using System.Data;

//Apache 2.0 License: https://github.com/StackExchange/dapper-dot-net/blob/master/License.txt
namespace ServiceStack.OrmLite.Dapper
{
    /// <summary>
    /// Handles variances in features per DBMS
    /// </summary>
    class FeatureSupport
    {
        private static readonly FeatureSupport
            Default = new FeatureSupport(false),
            Postgres = new FeatureSupport(true);

        /// <summary>
        /// Gets the feature set based on the passed connection
        /// </summary>
        public static FeatureSupport Get(IDbConnection connection)
        {
            string name = connection != null ? connection.GetType().Name : null;
            if (string.Equals(name, "npgsqlconnection", StringComparison.OrdinalIgnoreCase)) return Postgres;
            return Default;
        }
        private FeatureSupport(bool arrays)
        {
            Arrays = arrays;
        }
        /// <summary>
        /// True if the db supports array columns e.g. Postgresql
        /// </summary>
        public bool Arrays { get; set; }
    }
}
