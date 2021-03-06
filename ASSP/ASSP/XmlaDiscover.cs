/*============================================================================
  File:    XmlaDiscover.cs

  Summary: The primary purpose of this class is to execute XMLA discover
           commands and to return the results as a DataTable. It also has some
           secondary methods that execute the Cancel and ClearCache commands.

  Date:    March 25, 2007

  ----------------------------------------------------------------------------
  This file is part of the Analysis Services Stored Procedure Project.
  http://www.codeplex.com/Wiki/View.aspx?ProjectName=ASStoredProcedures
  
  THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
  PARTICULAR PURPOSE.
============================================================================*/
using System;
using System.Data;
using Microsoft.AnalysisServices.Xmla;
using Microsoft.AnalysisServices.AdomdServer;
using Microsoft.AnalysisServices;
using System.Text;
using System.Collections.Generic;
using System.Xml;

namespace ASStoredProcs
{
    public class XmlaDiscover
    {

#region Discover Functions

        [SafeToPrepare(true)]
        public DataTable Discover(string request)
        {
            return Discover(request, "", "");
        }

        [SafeToPrepare(true)]
        public DataTable Discover(string request, string restrictions)
        {
            return Discover(request, restrictions, "");
        }

        [SafeToPrepare(true)]
        public DataTable Discover(string request, string restrictions, string properties) 
        {
            Context.TraceEvent(100, 0, "Discover: Starting ("+ request +")");
            
            XmlaClient client = createXmlaClientAndConnect();
            DataTable dt = new DataTable();
            // if no properties restriction is specified, default to using
            // the current database
            if (properties.Length == 0)
            {
                properties = string.Format("<CATALOG>{0}</CATALOG>", Context.CurrentDatabaseName);
            }
            try
            {
                string res;
                TimeoutUtility.XmlaClientDiscover(client, request, restrictions, properties, out res, false, false, false);

                dt = createDataTableFromXmla(res);
            }
            finally
            {
                client.Disconnect();
                Context.TraceEvent(100, 0, "Discover: XML/A Connection disconnected");
            }
            Context.TraceEvent(100,0,"Discover: Finished (" + dt.Rows.Count.ToString() + " rows returned");
            return dt;

        }

        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadataFull(string path)
        {
            return DiscoverXmlMetadataFull(path, "","");
        }

        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadataFull(string path,string whereClause)
        {
            return DiscoverXmlMetadataFull(path,whereClause, "");
        }

        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadataFull(string path, string whereClause, string restrictions)
        {
            XmlaClient xmlac = createXmlaClientAndConnect();
            string xmlaResult;
            string properties = "";

            TimeoutUtility.XmlaClientDiscover(xmlac, "DISCOVER_XML_METADATA", restrictions, properties, out xmlaResult, false, false, false);

            XmlaDiscoverParser dp = new XmlaDiscoverParser();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlaResult);
            return dp.Parse(doc, path, Context.ExecuteForPrepare , whereClause);
        }

        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadata(string path)
        {
            return DiscoverXmlMetadata(path, "", "");
        }
        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadata(string path, string whereClause)
        {
            return DiscoverXmlMetadata(path, whereClause, "");
        }
        [SafeToPrepare(true)]
        public DataTable DiscoverXmlMetadata(string path, string whereClause, string restrictions)
        {
            if (restrictions.Contains("DatabaseID"))
            {
                throw new ArgumentException("You cannot pass a DatabaseID to the DiscoverXmlMetadata function, use the DiscoverMetadataFull function instead.");
            }
            restrictions = "<DatabaseID>" + GetDatabaseIDFromName(Context.CurrentDatabaseName) + "</DatabaseID>";

            return DiscoverXmlMetadataFull(path, whereClause, restrictions);
        }

#endregion

#region DiscoverSingleValue Functions

        public object DiscoverSingleValue(string column, string request)
        {
            return GetSingleValueFromDataTable(column, Discover(request));
        }

        public object DiscoverSingleValue(string column, string request, string restrictions)
        {
            return GetSingleValueFromDataTable(column, Discover(request, restrictions));
        }

        public object DiscoverSingleValue(string column, string request, string restrictions, string properties)
        {
            return GetSingleValueFromDataTable(column, Discover(request, restrictions, properties));
        }

        public object DiscoverXmlMetadataFullSingleValue(string column, string path)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadataFull(path));
        }

        public object DiscoverXmlMetadataFullSingleValue(string column, string path, string whereClause)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadataFull(path, whereClause));
        }

        public object DiscoverXmlMetadataFullSingleValue(string column, string path, string whereClause, string restrictions)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadataFull(path, whereClause, restrictions));
        }

        public object DiscoverXmlMetadataSingleValue(string column, string path)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadata(path));
        }

        public object DiscoverXmlMetadataSingleValue(string column, string path, string whereClause)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadata(path, whereClause));
        }

        public object DiscoverXmlMetadataSingleValue(string column, string path, string whereClause, string restrictions)
        {
            return GetSingleValueFromDataTable(column, DiscoverXmlMetadata(path, whereClause, restrictions));
        }

        public object DMVSingleValue(string column, string discoverRowset)
        {
            return GetSingleValueFromDataTable(column, DMV(discoverRowset));
        }

        public object DMVSingleValue(string column, string discoverRowset, string restrictions)
        {
            return GetSingleValueFromDataTable(column, DMV(discoverRowset, restrictions));
        }

        public object DMVSingleValue(string column, string discoverRowset, string restrictions, string properties)
        {
            return GetSingleValueFromDataTable(column, DMV(discoverRowset, restrictions, properties));
        }

#endregion

#region Private Helper Functions

        private string GetDatabaseIDFromName(string databaseName)
        {
            DataTable dt = DiscoverXmlMetadataFull(@"\Server\Databases\Database","","<ObjectExpansion>ExpandObject</ObjectExpansion>");
            DataRow[] dr = dt.Select("Name='" + Context.CurrentDatabaseName + "'");
            string databaseID = (string)dr[0].ItemArray[dt.Columns.IndexOf("ID")];
            return databaseID;
        }

        private XmlaClient createXmlaClientAndConnect()
        {
            Context.CheckCancelled();
            XmlaClient client;
            client = new XmlaClient();
            Context.TraceEvent(100,0,"Discover: About to Establish XML/A Connection");
            client.Connect(Context.CurrentServerID);
            Context.TraceEvent(100, 0, "Discover: XML/A Connection established");
            return client;
        }

        // This routine is fairly "brute force", creating an XmlDocument and 
        // then iterating through the nodes
        private DataTable createDataTableFromXmla(string xmlaResult)
        {
            DataTable dt = null;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlaResult);
            
            //               return         root       schema/rows       Look for element with attribute "name" = row
            //                  ^             ^             ^             ^
            //xmlDom.ChildNodes[0].ChildNodes[0].ChildNodes[0].ChildNodes[0].Attributes.Count
            foreach (XmlNode n in doc.ChildNodes[0].ChildNodes[0].ChildNodes)
            {
                Context.CheckCancelled();

                if ((n.NodeType == XmlNodeType.Element) && (n.LocalName == "schema"))
                {
                    dt = buildTableFromSchema(n);
                    if (Context.ExecuteForPrepare)
                    {
                        // If this is a prepare call then we only need the table structure
                        // and can exit the loop here.
                        break; 
                    }
                }
                if ((n.NodeType == XmlNodeType.Element) && (n.LocalName == "row"))
                {
                    // processRows
                    addTableRow(dt, n);
                }
            }            
            return dt;
        }

        private DataTable buildTableFromSchema(XmlNode n)
        {
            Context.TraceEvent(100, 0, "Discover: Starting to build result table structure");
            DataTable dt = new DataTable();

            foreach (XmlNode n2 in n.ChildNodes)
            {
                Context.CheckCancelled(); // Check if the user has cancelled 

                System.Diagnostics.Debug.WriteLine(n.Value);
                if ((n2.NodeType == XmlNodeType.Element) && (n2.LocalName == "complexType"))
                {
                    foreach (XmlAttribute a in n2.Attributes)
                    {
                        if ((a.Name == "name") && (a.Value == "row"))
                        {
                            // we have the row definition
                            //                                  Sequence
                            //                                   ^
                            foreach (XmlNode n3 in n2.ChildNodes[0].ChildNodes)
                            {
                                string fld = "";
                                Type typ = null;
                                foreach (XmlAttribute a2 in n3.Attributes)
                                {
                                    Context.CheckCancelled(); // Check if the user has cancelled 

                                    switch (a2.Name)
                                    {
                                        case "sql:field":
                                            fld = a2.Value;
                                            break;
                                        case "type":
                                            switch (a2.Value)
                                            {
                                                case "xsd:boolean":
                                                    typ = typeof(bool);
                                                    break;
                                                case "xsd:unsignedShort":
                                                case "xsd:short":
                                                case "xsd:int":
                                                case "int":
                                                    typ = typeof(int);
                                                    break;
                                                case "xsd:unsignedLong":
                                                case "xsd:unsignedInt":
                                                case "long":
                                                    typ = typeof(long);
                                                    break;
                                                case "xsd:datetime":
                                                case "datetime":
                                                    typ = typeof(DateTime);
                                                    break;
                                                case "xsd:string":
                                                case "string":
                                                default:
                                                    typ = typeof(string);
                                                    break;
                                            }


                                            break;
                                    }

                                }
                                if ((typ != null) && (fld != null))
                                {
                                    dt.Columns.Add(fld, typ);
                                }
                                // reset field and type variables
                                fld = null;
                                typ = null;
                            }
                        }
                    }
                }
            }
            Context.TraceEvent(100, 0, "Discover: Finished building result table structure");
            return dt;
        }//buildTableFromSchema

        private void addTableRow(DataTable dt, XmlNode n)
        {
            DataRow dr = dt.NewRow();
            foreach (XmlNode e in n.ChildNodes)
            {
                Context.CheckCancelled(); // Check if the user has cancelled 

                if (dt.Columns.Contains(e.LocalName))
                {
                    dr[e.LocalName] = getNodeText(e);
                }
            }
            dt.Rows.Add(dr);
        }

        private string getNodeText(XmlNode e)
        {
            if (e.ChildNodes.Count == 1 && e.FirstChild.LocalName == "Object")
            {
                StringBuilder sb = new StringBuilder();
                foreach (XmlNode ids in e.FirstChild.ChildNodes)
                {
                    sb.Append(ids.LocalName);
                    sb.Append("=");
                    sb.Append(ids.InnerText);
                    sb.Append(";");
                }
                return sb.ToString();
            }
            return e.InnerText;
        }

        private object GetSingleValueFromDataTable(string column, DataTable table)
        {
            if (table == null || table.Rows.Count == 0 || !table.Columns.Contains(column))
            {
                return null;
            }
            else
            {
                return table.Rows[0][column];
            }
        }
#endregion

#region Common Discover Functions
        [SafeToPrepare(true)]
        public DataTable DiscoverSessions()
        {
            return Discover("DISCOVER_SESSIONS");
        }

        [SafeToPrepare(true)]
        public DataTable DiscoverConnections()
        {
            return Discover("DISCOVER_CONNECTIONS");
        }

        [SafeToPrepare(true)]
        public DataTable DiscoverRowsets()
        {
            return Discover("DISCOVER_SCHEMA_ROWSETS");
        }

#endregion

#region ClearCache
        const string CLEARCACHE_TEMPLATE = "<Batch xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ClearCache><Object><DatabaseID>{0}</DatabaseID>{1}</Object></ClearCache></Batch>";
        const string CUBEID_TEMPLATE = "<CubeID>{0}</CubeID>";

        public void ClearCache()
        {
            ClearCache("");
        }

        // Clears the cache for the current database
        // This method uses Server.Execute() as I need to establish an AMO
        // connection anyway in order to find the CubeID from the CubeName
        // Other methods in this class use the XmlaClient objects to execute
        // the Xmla requests.
        public void ClearCache(string cubeName)
        {
            // only way to get a DatabaseID from a Database name appears to be to use AMO
            string dbId = "";
            Microsoft.AnalysisServices.Server svr = new Microsoft.AnalysisServices.Server();
            using (svr)
            {
                svr.Connect(Context.CurrentServerID);
                try
                {
                    Database db = svr.Databases.FindByName(Context.CurrentDatabaseName);
                    dbId = db.ID;
                    if (cubeName.Length != 0)
                    {
                        Cube c = db.Cubes.FindByName(cubeName);
                        if (c != null)
                        {
                            cubeName = "<CubeID>" + c.ID + "</CubeID>";
                        }
                        else
                        {
                            throw new Exception("The cube '" + cubeName + "' does not exist in the " + Context.CurrentDatabaseName + " database.");
                        }
                    }

                    // execute clear cache based on the ID
                    string clearCmd = string.Format(CLEARCACHE_TEMPLATE, dbId, cubeName);
                    svr.Execute(clearCmd);
                }
                catch 
                {   // re-throw any exception
                    throw;
                }
                finally
                {
                    // clean up
                    svr.Disconnect();
                }
            }
        }

#endregion

#region Cancel Functions
        // NOTE: None of these function have calls to Context.TraceEvent as they are simple wrappers around XML/A 
        //       calls anyway which will be visible in SQL Profiler.


        const string CANCEL_TEMPLATE = "<Cancel xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">{0}</Cancel>";
        
        // Cancel based on SPID
        public void CancelSPID(int spid)
        {
            string cancelCmd = string.Format(CANCEL_TEMPLATE, "<SPID>" + spid.ToString() + "</SPID>");
            executeCancel(cancelCmd);
        }

        // Cancel based on session guid
        public void CancelSession(string sessionGuid)
        {
            string cancelCmd = string.Format(CANCEL_TEMPLATE, "<SessionID>" + sessionGuid + "</SessionID>");
            executeCancel(cancelCmd);
        }
        
        //Cancel based on connection id
        public void CancelConnection(int connectionId)
        {
            string cancelCmd = string.Format(CANCEL_TEMPLATE, "<ConnectionID>" + connectionId.ToString() + "</ConnectionID>");
            executeCancel(cancelCmd);
        }

        // Cancel Helper function
        private void executeCancel(string cancelCmd)
        {
            XmlaClient client = createXmlaClientAndConnect();
            string res = string.Empty;
            try
            {
                client.Execute(cancelCmd, string.Empty, out res, false, true);
            }
            finally
            {
                client.Disconnect();
            }
        }

#endregion


#region DMV functions
        [SafeToPrepare(true)]
        public DataTable DMV(String discoverRowset)
        {
            return DMV(discoverRowset, string.Empty, string.Empty);
        }

        [SafeToPrepare(true)]
        public DataTable DMV(String discoverRowset, String restrictions)
        {
            return DMV(discoverRowset, restrictions, string.Empty);
        }

        [SafeToPrepare(true)]
        public DataTable DMV(String discoverRowset, String restrictions, String properties)
        {
            if (discoverRowset.Trim().StartsWith("SELECT ", StringComparison.InvariantCultureIgnoreCase))
            {
                DMVParser.SelectParser sp = new DMVParser.SelectParser();
                sp.Parse(discoverRowset);
                if (restrictions.Length == 0)
                {
                    restrictions = sp.Restrictions;
                }
                return DiscoverView(sp.FromClause,restrictions, properties, sp.WhereClause, sp.OrderByClause, sp.Distinct, sp.Columns);
            }
            else
            {
                return DiscoverView(discoverRowset, restrictions, properties,  string.Empty, string.Empty, false, new string[0]);
            }

        }


        [SafeToPrepare(true)]
        private DataTable DiscoverView(String discoverRowset, String restrictions, String properties, String WhereClause, String SortBy, bool distinct, string[] columns)
        {
            DataTable dt = Discover(discoverRowset,restrictions,properties);
            DataView dv = dt.DefaultView;
            dv.RowFilter = WhereClause;
            dv.Sort = SortBy;
            if (columns.Length == 0)
            {
                return dv.ToTable();
            }
            else
            {
                return dv.ToTable(distinct, columns);
            }
        }
#endregion

        #region ForEach functions
        [SafeToPrepare(true)]
        public DataTable ForEachMeasureGroup(string command)
        {
            return ForEachMeasureGroupInternal(command, false);
        }

        [SafeToPrepare(true)]
        public DataTable ForEachMeasureGroupInternal(string command, bool forEachPartition)
        {
            DataTable result = new DataTable();

            Microsoft.AnalysisServices.Server server = new Microsoft.AnalysisServices.Server();
            server.Connect("*");
            Database db = server.Databases.GetByName(Context.CurrentDatabaseName);

            foreach (Microsoft.AnalysisServices.Cube c in db.Cubes)
            {
                Microsoft.AnalysisServices.AdomdClient.AdomdConnection conn = TimeoutUtility.ConnectAdomdClient("Data Source=" + server.Name + ";Initial Catalog=" + Context.CurrentDatabaseName + ";Cube=" + c.Name);

                foreach (Microsoft.AnalysisServices.MeasureGroup mg in c.MeasureGroups)
                {
                    if (forEachPartition)
                    {
                        foreach (Microsoft.AnalysisServices.Partition p in mg.Partitions)
                        {
                            //parameters don't appear to work with some DMV queries, so use string substitution
                            string sNewCommand = command;
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "DATABASE_NAME", db.Name);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "DATABASE_ID", db.ID);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "CUBE_NAME", c.Name);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "CUBE_ID", c.ID);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "MEASUREGROUP_NAME", mg.Name);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "MEASUREGROUP_ID", mg.ID);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "PARTITION_NAME", p.Name);
                            sNewCommand = ReplaceParameterWithString(sNewCommand, "PARTITION_ID", p.ID);
                            Microsoft.AnalysisServices.AdomdClient.AdomdCommand cmd = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand(sNewCommand, conn);
                            cmd.CommandTimeout = 0;
                            Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter adp = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter(cmd);
                            TimeoutUtility.FillAdomdDataAdapter(adp, result);
                        }
                    }
                    else
                    {
                        //parameters don't appear to work with some DMV queries, so use string substitution
                        string sNewCommand = command;
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "DATABASE_NAME", db.Name);
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "DATABASE_ID", db.ID);
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "CUBE_NAME", c.Name);
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "CUBE_ID", c.ID);
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "MEASUREGROUP_NAME", mg.Name);
                        sNewCommand = ReplaceParameterWithString(sNewCommand, "MEASUREGROUP_ID", mg.ID);
                        Microsoft.AnalysisServices.AdomdClient.AdomdCommand cmd = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand(sNewCommand, conn);
                        cmd.CommandTimeout = 0;
                        Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter adp = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter(cmd);
                        TimeoutUtility.FillAdomdDataAdapter(adp, result);
                    }
                }

                conn.Close();
            }
            return result;
        }

        private string ReplaceParameterWithString(string command, string parameterName, string value)
        {
            return command.Replace("@" + parameterName, "'" + value.Replace("'", "''") + "'");
        }
        
        [SafeToPrepare(true)]
        public DataTable ForEachPartition(string command)
        {
            return ForEachMeasureGroupInternal(command, true);
        }
        #endregion
    } // XmlaDiscover class


}