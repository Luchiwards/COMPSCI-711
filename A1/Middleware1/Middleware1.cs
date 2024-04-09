using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


public class Program
{
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
    }
}

public class Form1 : Form
{
    // Decalre and initialize variables
    private Button button1;
    private RichTextBox richTextBox_sent;
    private RichTextBox richTextBox_received;
    private RichTextBox richTextBox_ready;
    private TcpListener listener;
    private TcpListener mlistener;
    private Label label_sent;
    private Label label_received;
    private Label label_ready;
    private int port = 8082;
    private int count = 1;
    private string title = "Middleware 1";
    private int middleware_id = 1;
    private int middleware_port = 8087;
    private int[] middleware_ports = { 8088, 8089, 8090, 8091 };
    private List<MMessages> messages_list = new List<MMessages>();
    private List<MMessage> ready_messages = new List<MMessage>();


    public Form1()
    {
        this.Text = title;
        this.Size = new System.Drawing.Size(730, 400);
        button1 = new Button();
        button1.Size = new System.Drawing.Size(100, 50);
        button1.Location = new System.Drawing.Point(35, 15);
        button1.Text = "Send Message";
        button1.Click += new EventHandler(button1_Click);

        label_sent = new Label();
        label_sent.Text = "Sent:";
        label_sent.Location = new System.Drawing.Point(40, 95);

        richTextBox_sent = new RichTextBox();
        richTextBox_sent.Location = new System.Drawing.Point(35, 120);
        richTextBox_sent.Width = 200;
        richTextBox_sent.Height = 200;
        richTextBox_sent.Multiline = true;
        richTextBox_sent.ScrollBars = RichTextBoxScrollBars.Vertical;

        label_received = new Label();
        label_received.Text = "Received:";
        label_received.Location = new System.Drawing.Point(260, 95);

        richTextBox_received = new RichTextBox();
        richTextBox_received.Location = new System.Drawing.Point(255, 120);
        richTextBox_received.Width = 200;
        richTextBox_received.Height = 200;
        richTextBox_received.Multiline = true;
        richTextBox_received.ScrollBars = RichTextBoxScrollBars.Vertical;

        label_ready = new Label();
        label_ready.Text = "Ready:";
        label_ready.Location = new System.Drawing.Point(480, 95);

        richTextBox_ready = new RichTextBox();
        richTextBox_ready.Location = new System.Drawing.Point(475, 120);
        richTextBox_ready.Width = 200;
        richTextBox_ready.Height = 200;
        richTextBox_ready.Multiline = true;
        richTextBox_ready.ScrollBars = RichTextBoxScrollBars.Vertical;

        Controls.Add(button1);
        Controls.Add(label_sent);
        Controls.Add(richTextBox_sent);
        Controls.Add(label_received);
        Controls.Add(richTextBox_received);
        Controls.Add(label_ready);
        Controls.Add(richTextBox_ready);

        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        mlistener = new TcpListener(IPAddress.Any, middleware_port);
        mlistener.Start();
        ListenForClientsAsync();
        mListenForClientsAsync();
    }

    // Set listener for network messages
    private async void ListenForClientsAsync()
    {
        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            ReadMessageAsync(client);
        }
    }

    // Set listener for middleware messages
    private async void mListenForClientsAsync()
    {
        while (true)
        {
            TcpClient client = await mlistener.AcceptTcpClientAsync();
            ReadMsgMiddlewares(client);
        }
    }

    // Read messages from the tcp client for the network, process the stream to plain text for the recieved box.
    private async void ReadMessageAsync(TcpClient client)
    {
        byte[] buffer = new byte[1024];
        await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
        string message = Encoding.UTF8.GetString(buffer).Trim('\0');
        richTextBox_received.AppendText($"{message}\n");
        // Insert the recieved message to a recieved list
        InsertMsgToList(message);

    }

    // Async event triggered by the send button, creates the message to be sent to the network, adds 1 to the counter of messages sent
    private async void button1_Click(object sender, EventArgs e)
    {
        TcpClient client = new TcpClient("localhost", 8081);
        string message = $"Msg #{count} from {title} {port}\n";
        byte[] data = Encoding.UTF8.GetBytes(message);
        await client.GetStream().WriteAsync(data, 0, data.Length);
        richTextBox_sent.AppendText(message);
        count += 1;

    }

    // Multicasts a message to all the middleware with the respective id and the timestamp for the recieved message from the network, adds a confirmation used later for the total ordering
    private async void SendMsgMiddlewares(string message, bool confirm)
    {
        int[] sortedPorts = new int[4];
        long unixTimestampMillis = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
        Array.Copy(middleware_ports, sortedPorts, 4);

        Array.Sort(sortedPorts);
        for (int i = 0; i < 4; i++)
        {

            TcpClient client = new TcpClient("localhost", sortedPorts[i]);
            byte[] data = Encoding.UTF8.GetBytes($"{message},{unixTimestampMillis},{confirm}");
            await client.GetStream().WriteAsync(data, 0, data.Length);
        }
    }

    // Multicasts a confirmation message to all the middleware when the definitive timestamp has been decided by total ordering.
    private async void SendMsgMiddlewares(string message, long unixTimestampMillis, bool confirm)
    {
        int[] sortedPorts = new int[4];
        Array.Copy(middleware_ports, sortedPorts, 4);

        Array.Sort(sortedPorts);
        for (int i = 0; i < 4; i++)
        {

            TcpClient client = new TcpClient("localhost", sortedPorts[i]);
            byte[] data = Encoding.UTF8.GetBytes($"{message},{unixTimestampMillis},{confirm}");
            await client.GetStream().WriteAsync(data, 0, data.Length);
        }
    }

    // Reads the message that comes from all the middlewares and processes the stream to plain text, gets the message, the clock and the confirmation variable from the stream
    private async void ReadMsgMiddlewares(TcpClient client)
    {
        byte[] buffer = new byte[1024];
        await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
        string[] message = Encoding.UTF8.GetString(buffer).Trim('\0').Split(',');
        string msg = message[0];
        long clock = long.Parse(message[1]);
        bool confirm = bool.Parse(message[2]);
        // Adds the message to the recieved or the ready list depending if its a confirmation message
        InsertMsgToList(msg, clock, confirm);

    }

    // It processes the message to get the identifiers (Msg#, Middleware), also creates a timestamp and stores it into a message object
    private void InsertMsgToList(string message)
    {
        long unixTimestampMillis = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();

        int msgId = Regex.Matches(message, "#(\\d+)")
                            .Cast<Match>()
                            .Select(m => int.Parse(m.Groups[1].Value))
                            .FirstOrDefault();

        int middlewareId = Regex.Matches(message, "Middleware (\\d)")
                                         .Cast<Match>()
                                         .Select(m => int.Parse(m.Groups[1].Value))
                                         .FirstOrDefault();

        int fromPort = Regex.Matches(message, "(\\d{4})$")
                                   .Cast<Match>()
                                   .Select(m => int.Parse(m.Groups[1].Value))
                                   .FirstOrDefault();

        MMessage newMsg = new MMessage(message, unixTimestampMillis);


        int messagesIndex = messages_list.FindIndex(m => m.id == msgId && m.middlewareId == middlewareId);

        // Checks if there are other messages that belong to the same list with the same message id and middleware id to store them 
        // so total order can be processed later
        if (messagesIndex != -1)
        {
            messages_list[messagesIndex].messages.Add(newMsg);
        }
        else
        {
            MMessages newMMessages = new MMessages(msgId, middlewareId);
            newMMessages.messages.Add(newMsg);
            messages_list.Add(newMMessages);

        }

        // Multicast the message with the inserted timestamp
        SendMsgMiddlewares(message, false);

    }

    // Processes the messages from the multicast and gets its identifiers, port and the middleware it belongs to. It also recieves the timestamp from the message as well as if
    // its a confirmation message or not.
    private void InsertMsgToList(string message, long unixTimestampMillis, bool confirm)
    {
        // Checks if its a confirmation message belonging to the Ready column
        if (confirm)
        {
            InsertReadyMsg(message, unixTimestampMillis);
        }
        else 
        {
            int msgId = Regex.Matches(message, "#(\\d+)")
                                .Cast<Match>()
                                .Select(m => int.Parse(m.Groups[1].Value))
                                .FirstOrDefault();

            int middlewareId = Regex.Matches(message, "Middleware (\\d)")
                                             .Cast<Match>()
                                             .Select(m => int.Parse(m.Groups[1].Value))
                                             .FirstOrDefault();

            int fromPort = Regex.Matches(message, "(\\d{4})$")
                                       .Cast<Match>()
                                       .Select(m => int.Parse(m.Groups[1].Value))
                                       .FirstOrDefault();

            MMessage newMsg = new MMessage(message, unixTimestampMillis);


            int messagesIndex = messages_list.FindIndex(m => m.id == msgId && m.middlewareId == middlewareId);

            // Checks if there are other messages that belong to the same list with the same message id and middleware id to store them 
            // so total order can be processed
            if (messagesIndex != -1)
            {
                messages_list[messagesIndex].messages.Add(newMsg);
            }
            else
            {
                MMessages newMMessages = new MMessages(msgId, middlewareId);
                newMMessages.messages.Add(newMsg);
                messages_list.Add(newMMessages);

            }

            // Confirm the message has been stored
            messagesIndex = messages_list.FindIndex(m => m.id == msgId && m.middlewareId == middlewareId); // Re-find or update index as needed

            // Checks if all the messages from the multicast have arrived so it can compare the timestamps so it can use the rules of totalordering
            if (messagesIndex != -1 && messages_list[messagesIndex].messages.Count == 5 && middleware_id == middlewareId)
            {
                messages_list[messagesIndex].messages.Sort((message1, message2) => message1.clock.CompareTo(message2.clock));

                MMessage largestClockMsg = messages_list[messagesIndex].messages.LastOrDefault();

                // Inserts the message to the Ready column
                InsertReadyMsg(largestClockMsg.message, largestClockMsg.clock);

                // Multicast a confirmation message to all the middlewares
                SendMsgMiddlewares(largestClockMsg.message, largestClockMsg.clock, true);
            }
        }

    }

    // Inserts the message to the GUI of the Ready column
    private void InsertReadyMsg(string message, long unixTimestampMillis)
    {
        ready_messages.Add(new MMessage(message, unixTimestampMillis));

        ready_messages.Sort((message1, message2) => message1.clock.CompareTo(message2.clock));

        string messagesString = String.Join("\n", ready_messages.Select(m => m.message));

        richTextBox_ready.Text = messagesString;
    }
}

// MMessages Stores the lists of Multicast messages.
public class MMessages
{
    public int id { get; set; }
    public int middlewareId { get; set; }
    public int count { get; set; }
    public List<MMessage> messages { get; set; }

    public MMessages(int id, int middlewareId)
    {
        this.id = id;
        this.middlewareId = middlewareId;
        this.count = 0;
        this.messages = new List<MMessage>();
    }
}

// MMessage is the object that stores the timestamp and message of all messages recieved by the Middleware.
public class MMessage
{
    public string message { get; set; }
    public long clock { get; set; }

    public MMessage(string message, long clock)
    {
        this.message = message;
        this.clock = clock;
    }
}
