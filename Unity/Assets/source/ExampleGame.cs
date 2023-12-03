using System.Net;

using Tungsten.Console;

using UnityEngine;
using UnityEngine.UI;

public class ExampleGame : MonoBehaviour {

    [SerializeField] private Text _ipText;
    [SerializeField] private Image _image;

    private void Awake() {
        _ipText.text = GetLocalIPAddress();
    }

    private void Start() {
        Console.AddCommand("SetColorRed", SetColorRed, "Set color to red");
        Console.AddCommand("SetColorGreen", SetColorGreen, "Set color to green");
        Console.AddCommand("SetColorBlue", SetColorBlue, "Set color to blue");
    }

    private void SetColorRed(string[] args) {
        _image.color = Color.red;
    }

    private void SetColorGreen(string[] args) {
        _image.color = Color.green;
    }

    private void SetColorBlue(string[] args) {
        _image.color = Color.blue;
    }

    public static string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList) {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                return ip.ToString();
            }
        }

        return string.Empty;
    }

}