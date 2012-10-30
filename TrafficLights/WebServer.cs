using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace TrafficLights
{
    public class WebServer : IDisposable
    {
        private Socket socket = null;  
        //open connection to onbaord led so we can blink it with every request

        TrafficLight green = new TrafficLight(new OutputPort(Pins.GPIO_PIN_D0, false));
        TrafficLight amber = new TrafficLight(new OutputPort(Pins.GPIO_PIN_D1, false));
        TrafficLight red = new TrafficLight(new OutputPort(Pins.GPIO_PIN_D2, false));
        private Thread greenThread;
        private Thread amberThread;
        private Thread redThread;

        public WebServer()
        {
            greenThread = new Thread(green.LightControl);
            greenThread.Start();
            amberThread = new Thread(amber.LightControl);
            amberThread.Start();
            redThread = new Thread(red.LightControl);
            redThread.Start();
            //Initialize Socket class
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Request and bind to an IP from DHCP server
            socket.Bind(new IPEndPoint(IPAddress.Any, 80));
            string address =
                Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress;
            //Debug print our IP address
            while (address == "192.168.5.100")
            {
                address =
                Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress;
                Thread.Sleep(2000);
            }
            Debug.Print(address);
            //Start listen for web requests
            socket.Listen(10);

            ListenForRequest();
        }

        public void ListenForRequest()
        {
            while (true)
            {                
                using (Socket clientSocket = socket.Accept())
                {
                    Thread.Sleep(200);
                    IPEndPoint clientIP = clientSocket.RemoteEndPoint as IPEndPoint;
                    EndPoint clientEndPoint = clientSocket.RemoteEndPoint;
                    int bytesReceived = clientSocket.Available;

                    if (bytesReceived > 0)
                    {
                        byte[] buffer = new byte[bytesReceived];
                        int byteCount = clientSocket.Receive(buffer, bytesReceived, SocketFlags.None);
                        string request = new string(Encoding.UTF8.GetChars(buffer));
                        if(clientSocket.Available > 0)
                        {
                            bytesReceived = clientSocket.Available;
                            buffer = new byte[clientSocket.Available];
                            byteCount = clientSocket.Receive(buffer, bytesReceived, SocketFlags.None);
                            request += new string(Encoding.UTF8.GetChars(buffer));
                        }
                        string response;
                        string header;
                        try
                        {
                            var trafficLightRequest = ParseRequest(request);
                            Debug.Print(trafficLightRequest.Colour.ToString());
                            Debug.Print("Flashing: " + trafficLightRequest.Flashing.ToString());
                            Debug.Print("Solo: " + trafficLightRequest.Solo.ToString());
                            SetLed(trafficLightRequest);
                            response = @"{""colour"":"""+trafficLightRequest.Colour+@""", ""flashing"":"+trafficLightRequest.Flashing.ToString()+@",""solo"":"+trafficLightRequest.Solo.ToString()+"}";
                            header = "HTTP/1.0 200 OK\r\nContent-Type: text; charset=utf-8\r\nContent-Length: " + response.Length.ToString() + "\r\nConnection: close\r\n\r\n";
                            clientSocket.Send(Encoding.UTF8.GetBytes(header), header.Length, SocketFlags.None);
                            clientSocket.Send(Encoding.UTF8.GetBytes(response), response.Length, SocketFlags.None);
                        }
                        catch (Exception)
                        {
                            response = string.Empty;
                            header = "HTTP/1.0 404 OK\r\nContent-Type: text; charset=utf-8\r\nContent-Length: " + response.Length.ToString() + "\r\nConnection: close\r\n\r\n";
                            clientSocket.Send(Encoding.UTF8.GetBytes(header), header.Length, SocketFlags.None);
                            clientSocket.Send(Encoding.UTF8.GetBytes(response), response.Length, SocketFlags.None);
                        }
                    }
                }
            }
        }

        public void SetLed(TrafficLightRequest request)
        {
            if(request.Solo)
            {
                green.State = TrafficLightState.Off;
                amber.State = TrafficLightState.Off;
                red.State = TrafficLightState.Off;               
            }
            if(request.Flashing)
            {
                switch(request.Colour)
                {
                    case(TrafficLightColour.Green):
                        green.State = TrafficLightState.Flashing;
                        break;
                    case(TrafficLightColour.Amber):
                        amber.State = TrafficLightState.Flashing;
                        break;
                    case(TrafficLightColour.Red):
                        red.State = TrafficLightState.Flashing;
                        break;
                }
            }
            else
            {
                switch (request.Colour)
                {
                    case (TrafficLightColour.Green):
                        green.State = TrafficLightState.On;
                        break;
                    case (TrafficLightColour.Amber):
                        amber.State = TrafficLightState.On;
                        break;
                    case (TrafficLightColour.Red):
                        red.State = TrafficLightState.On;
                        break;
                }
            }
        }
        
        public void Dispose()
        {
            if (socket != null)
            {
                socket.Close();
            }
        }

        private TrafficLightRequest ParseRequest(string request)
        {
            try
            {
                var trafficLightRequest = new TrafficLightRequest();
                foreach (var line in request.Split('\n'))
                {
                    string[] phrases = line.Split(' ');
                    string url = phrases[1].ToLower();
                    if(url.IndexOf("flashing=true") != -1)
                    {
                        trafficLightRequest.Flashing = true;
                    }
                    if (url.IndexOf("solo=true") != -1)
                    {
                        trafficLightRequest.Solo = true;
                    }
                    if(url.Substring(0,6) == "/green")
                        trafficLightRequest.Colour = TrafficLightColour.Green;
                    if (url.Substring(0, 4) == "/red")
                        trafficLightRequest.Colour = TrafficLightColour.Red;
                    if (url.Substring(0, 6) == "/amber")
                        trafficLightRequest.Colour = TrafficLightColour.Amber;
                    return trafficLightRequest;
                }
                if(trafficLightRequest.Colour == TrafficLightColour.NotSpecified)
                {
                    throw new InvalidOperationException("Must specify the colour");
                }
                return trafficLightRequest;
            }
            catch(Exception ex)
            {
                throw new NotSupportedException();    
            }
        }
    }

    public class TrafficLightRequest
    {
        public bool Flashing;
        public bool Solo;
        public TrafficLightColour Colour;

        public TrafficLightRequest()
        {
            Flashing = false;
            Solo = false;
            Colour = TrafficLightColour.NotSpecified;
        }
    }

    public enum TrafficLightColour
    {
        NotSpecified,
        Red,
        Amber,
        Green
    }
}

