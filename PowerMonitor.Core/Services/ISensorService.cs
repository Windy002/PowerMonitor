using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;

namespace PowerMonitor.Core.Services;

public interface ISensorService
{
    bool Initialize();
    List<ComponentPower> ReadSensors();
    HardwareInfo GetHardwareInfo();
    int ConsecutiveFailures { get; }
}
