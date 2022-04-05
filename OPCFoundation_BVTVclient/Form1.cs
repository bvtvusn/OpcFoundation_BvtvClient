using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Opc.Ua;   // Install-Package OPCFoundation.NetStandard.Opc.Ua
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Net;
using System.Net.Sockets;

namespace OPCFoundation_BVTVclient
{
    public partial class Form1 : Form
    {
        BVTV_OPC opcObject;

        ReferenceDescriptionCollection loadedItems;
        public NodeId curNodeId = ObjectIds.ObjectsFolder;
        Subscription subs_currentSelectedNode;
        public Form1()
        {
            InitializeComponent();
            ListIpAddresses();

        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            opcObject = new BVTV_OPC();
            string IP = cmbServerIP.Text;
            uint port = Convert.ToUInt32(numPort.Value);
            opcObject.StartSession(IP, port);
            //opcObject.StartSession(cmbServerIP.Text, 55105);

            // ----- Adding a subscription ------ //

            //var list = new List<MonitoredItem> {
            //        new MonitoredItem() { DisplayName = "ServerStatusCurrentTime", StartNodeId = "i=2258", AttributeId = 13}
            //        new MonitoredItem() { DisplayName = "test", StartNodeId = new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigIn_0"), AttributeId = 13}
            //    };
            //list[0].Notification += Form1_Notification; // Adding eventhandler that is run when the tag is updated
            //opcObject.addSubscription(list, 500);

            FillNavigationList();
        }

        
        public void ListIpAddresses()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                //if (ip.AddressFamily == AddressFamily.InterNetwork)
                //{
                    cmbServerIP.Items.Add(ip.ToString());
                //}
            }
            cmbServerIP.SelectedIndex = 0;
        }

        //private void Form1_Notification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        //{
        //    foreach (var value in item.DequeueValues())
        //    {
        //        txtShow.Invoke(new Action(() => txtShow.AppendText(item.DisplayName + " " + value.Value + " " + value.SourceTimestamp + " " + value.StatusCode + "\r\n")));
        //    }
        //}

        //private void btnWrite_Click(object sender, EventArgs e)
        //{
        //    opcObject.OPC_singleWrite(new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_0"), true);
        //}

        
        private void ListBox1_Click(object sender, EventArgs e)
        {
            ListBox lb = sender as ListBox;
            
            if (lb.SelectedIndex == -1) return;
            else if (lb.SelectedIndex == 0)
            {
                int index = lb.SelectedIndex;

                try { curNodeId = opcObject.BrowseUp(curNodeId); } // sets curNodeId to the parents nodeId
                catch {  }

                FillNavigationList();
            }
            else
            {
                int index = lb.SelectedIndex - 1;
                ExpandedNodeId tempId = loadedItems.ElementAt(index).NodeId;

                try { curNodeId = opcObject.ExpandedNodeId_ToNodeId(tempId); }
                catch { }

                FillNavigationList();
            }

            // ---- remove subscription ---- //
            opcObject.RemoveSubscription(subs_currentSelectedNode);

            // ---- add subscription ---- //

            var list = new List<MonitoredItem> {
                //new MonitoredItem() { DisplayName = "ServerStatusCurrentTime", StartNodeId = "i=2258", AttributeId = 13}
                //new MonitoredItem() { DisplayName = "test", StartNodeId = new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigIn_0"), AttributeId = 13}
                new MonitoredItem() {StartNodeId = curNodeId}
                };
            list[0].Notification += Form1_NodeValueChanged; // Adding eventhandler that is run when the tag is updated
            subs_currentSelectedNode = opcObject.addSubscription(list,500);
        }

        private void Form1_NodeValueChanged(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e) // Eventhandler. Runs when the OPC variable(s) is changed
        {
            foreach (DataValue value in monitoredItem.DequeueValues())
            {
                string valueData = monitoredItem.DisplayName + " " + value.Value + " " + value.SourceTimestamp + " " + value.StatusCode +  " " + value.WrappedValue.TypeInfo + "\r\n";
                txtValue.Invoke(new Action(() => txtValue.Text = valueData));
            }
        }

        void FillNavigationList()
        {
            listBox1.Items.Clear();
            listBox1.Items.Add("..");

            loadedItems = opcObject.GetChildren(curNodeId);
            
            foreach (ReferenceDescription rd in loadedItems)
            {
                string browsename = rd.BrowseName.ToString();
                BuiltInType datatype = opcObject.getDatatype(opcObject.ExpandedNodeId_ToNodeId(rd.NodeId));
                if (datatype != BuiltInType.Null)
                {
                    browsename += "\t\t" + datatype.ToString();
                }
                listBox1.Items.Add(browsename);
            }

            txtNodeData.Text = curNodeId.ToString();
        }

        private void BtnWrite_Click(object sender, EventArgs e)
        {
            opcObject.OPC_singleWrite(curNodeId, txtWrite.Text);
            //opcObject.OPC_singleWrite(new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_0"), false);
        }

        private void btnWriteTest_Click(object sender, EventArgs e)
        {
            List<NodeId> nodeList = new List<NodeId>();
            nodeList.Add(new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_0"));
            nodeList.Add(new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_1"));
            opcObject.OPC_Write(nodeList, new List<object> { false, false });
        }
    }
    public class BVTV_OPC
    {
        //
        // Made by Bjørn Vegard Tveraaen
        //
        ApplicationConfiguration config;
        EndpointDescription selectedEndpoint;
        Session session;
        public delegate void stringDelegate(string element);
        public event stringDelegate OPCupdate;

        public BVTV_OPC()
        {
            config = new ApplicationConfiguration()
            {
                ApplicationName = "MyHomework",
                ApplicationUri = Utils.Format(@"urn:{0}:MyHomework", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", "MyHomework", System.Net.Dns.GetHostName()) },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            var application = new ApplicationInstance
            {
                ApplicationName = "MyHomework",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            //application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();
        }

        //
        // BVTV_OPC class is made by Bjørn Vegard Tveraaen
        //

        /// <summary>
        /// Connects to the OPC server
        /// </summary>
        /// <param name="IP"> The IP address of the server</param>
        /// <param name="port"> The Network port on the server</param>
        /// <returns></returns>
        public void StartSession(string IP, uint port)
        {
            List<TreeNode> OPC_serverData = new List<TreeNode>();
            selectedEndpoint = CoreClientUtils.SelectEndpoint("opc.tcp://" + IP + ":" + port.ToString(), useSecurity: false, operationTimeout: 5000);
            session = Session.Create(config, new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config)), false, "", 60000, null, null).GetAwaiter().GetResult();

            ReferenceDescriptionCollection refs;
            Byte[] cp;
            session.Browse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
        }


        public ReferenceDescriptionCollection GetChildren(NodeId node)
        {
            ReferenceDescriptionCollection refs;

            Byte[] cp;
            session.Browse(null, null, node, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
            //refs = session.FetchReferences(node);
            if (refs.Count <= 0)
            {
                //throw new InvalidOperationException("Node Doesn't exist");
                return new ReferenceDescriptionCollection();
            }
            return refs;
        }

        /// <summary>
        /// Returns the parent NodeId
        /// </summary>
        /// <returns>The parent nodeId</returns>
        public NodeId BrowseUp(NodeId node)
        {
            ReferenceDescriptionCollection refs;

            Byte[] cp;
            session.Browse(null, null, node, 0u, BrowseDirection.Inverse, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
            
            if (refs.Count <= 0)
            {
                throw new InvalidOperationException("Node Doesn't exist");
            }
            NodeId parentNode = this.ExpandedNodeId_ToNodeId(refs.ElementAt(0).NodeId);
            
            return parentNode;
        }

        /// <summary>
        /// Adds subscriptions that listen for change in variables
        /// </summary>
        /// <param name="Items"> List of items to monitor</param>
        /// <param name="interval"> number of milliseconds between updates/// </param>
        public Subscription addSubscription(List<MonitoredItem> Items, int interval)
        {
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = interval };
            subscription.AddItems(Items);
            session.AddSubscription(subscription);
            subscription.Create();
            return subscription;
        }

        public void RemoveSubscription(Subscription _subscription)
        {
            if (_subscription != null)
            {
                session.RemoveSubscription(_subscription);
            }
            
        }
        /// <summary>
        /// Writes a single value in the OPC server
        /// </summary>
        /// <param name="nodeid">NodeId to write to</param>
        /// <param name="value">The value to write</param>
      
        public void OPC_singleWrite(NodeId nodeid, object value)
        {
            DataValue temp = ReadValue(nodeid);
            object convertedValue = ConvertDatatype(temp, value);
            

            WriteValue valueToWrite = new WriteValue();

            valueToWrite.NodeId = nodeid; // new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_0");
            valueToWrite.AttributeId = 13;// 13 = the value attribute ;m_attributeId; 
            valueToWrite.Value.Value = convertedValue; // false;// ChangeType();
            valueToWrite.Value.StatusCode = StatusCodes.Good;
            valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
            valueToWrite.Value.SourceTimestamp = DateTime.MinValue;
            WriteValueCollection valuesToWrite = new WriteValueCollection();
            valuesToWrite.Add(valueToWrite);
            // write current value.
            StatusCodeCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            ResponseHeader result = session.Write(
                null,
                valuesToWrite,
                out results,
                out diagnosticInfos);
        }

        /// <summary>
        /// Writes multiple values to the OPC server
        /// </summary>
        /// <param name="nodeids">NodeIds to write to</param>
        /// <param name="values">The values to write</param>
        public void OPC_Write(List<NodeId> nodeids, List<object> values)
        {
            int n = Math.Min(nodeids.Count, values.Count);

            WriteValueCollection valuesToWrite = new WriteValueCollection();

            for (int i = 0; i < n; i++)
            {
                DataValue temp = session.ReadValue(nodeids[i]);
                //session.Read(null,10000,nll,)
                object convertedValue = ConvertDatatype(temp, values[i]);
                WriteValue valueToWrite = new WriteValue();

                valueToWrite.NodeId = nodeids[i]; // new NodeId("ns=6;s=S71500ET200MP station_2.PLC_1.DigOut_0");
                valueToWrite.AttributeId = 13;// 13 = the value attribute ;m_attributeId; 
                valueToWrite.Value.Value = convertedValue; // false;// ChangeType();
                valueToWrite.Value.StatusCode = StatusCodes.Good;
                valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
                valueToWrite.Value.SourceTimestamp = DateTime.MinValue;
            
                valuesToWrite.Add(valueToWrite);
            }
            // write current value.
            StatusCodeCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            ResponseHeader result = session.Write(
                null,
                valuesToWrite,
                out results,
                out diagnosticInfos);
        }
        /// <summary>
        /// Converts ExpandedNodeId to NodeId
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public NodeId ExpandedNodeId_ToNodeId(ExpandedNodeId nodeId)
        {
            return ExpandedNodeId.ToNodeId(nodeId, session.NamespaceUris);
        }

        internal DataValue ReadValue(NodeId nodeToRead)
        {
            return session.ReadValue(nodeToRead);
            
        }

        internal BuiltInType getDatatype(NodeId nodeToRead)
        {
            try
            {
                return session.ReadValue(nodeToRead).WrappedValue.TypeInfo.BuiltInType;
            }
            catch (Exception)
            {
                return BuiltInType.Null;
            }
        }

        /// <summary>
        /// Changes the value in the string box to the data type required for the write operation.
        /// </summary>
        /// <returns>A value with the correct type.</returns>
        public object ConvertDatatype(DataValue m_value, object WriteString)
        {
            object value = (m_value != null) ? m_value.Value : null;

            switch (m_value.WrappedValue.TypeInfo.BuiltInType)
            {
                case BuiltInType.Boolean:
                    {
                        value = Convert.ToBoolean(WriteString);
                        break;
                    }

                case BuiltInType.SByte:
                    {
                        value = Convert.ToSByte(WriteString);
                        break;
                    }

                case BuiltInType.Byte:
                    {
                        value = Convert.ToByte(WriteString);
                        break;
                    }

                case BuiltInType.Int16:
                    {
                        value = Convert.ToInt16(WriteString);
                        break;
                    }

                case BuiltInType.UInt16:
                    {
                        value = Convert.ToUInt16(WriteString);
                        break;
                    }

                case BuiltInType.Int32:
                    {
                        value = Convert.ToInt32(WriteString);
                        break;
                    }

                case BuiltInType.UInt32:
                    {
                        value = Convert.ToUInt32(WriteString);
                        break;
                    }

                case BuiltInType.Int64:
                    {
                        value = Convert.ToInt64(WriteString);
                        break;
                    }

                case BuiltInType.UInt64:
                    {
                        value = Convert.ToUInt64(WriteString);
                        break;
                    }

                case BuiltInType.Float:
                    {
                        value = Convert.ToSingle(WriteString);
                        break;
                    }

                case BuiltInType.Double:
                    {
                        value = Convert.ToDouble(WriteString);
                        break;
                    }

                default:
                    {
                        value = WriteString;
                        break;
                    }
            }
            return value;
        }

        //
        // Made by Bjørn Vegard Tveraaen
        //
    }

}
