using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace TrafficLights
{
    public class TrafficLight
    {
        public TrafficLightState State;
        public OutputPort Led;

        public TrafficLight(OutputPort port)
        {
            Led = port;
        }

        public void LightControl()
        {
            while(true)
            {
                if(State == TrafficLightState.Flashing)
                {
                    Led.Write(!Led.Read());
                }
                else
                {
                    if(State == TrafficLightState.On)
                    {
                        Led.Write(true);
                    }
                    else
                    {
                        Led.Write(false);
                    }
                }
                Thread.Sleep(500);
            }
        }
    }

    public enum TrafficLightState
    {
        Off,
        On,
        Flashing
    }
}
