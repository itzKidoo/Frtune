# Tuning notes for your PC

**Intel Core i3-9100F · NVIDIA GTX 1650 Super · 16 GB RAM · desktop**

frtune detects this hardware itself, marks the matching tweaks **"For your PC"**, and includes them when you press **Optimize**. This page explains the reasoning so you can decide for yourself.

## Where the bottleneck is

The **i3-9100F is 4 cores / 4 threads** with no Hyper-Threading, 3.6 GHz base and 4.2 GHz turbo. The **GTX 1650 Super** (Turing, 4 GB GDDR6) is roughly GTX 1060 6 GB level. In most games released after 2018, the CPU becomes the limit before the GPU, and stutters (poor 1% lows) come from background work competing for those four threads.

So the tweaks that help most on this PC are the ones that **free CPU time** and **remove scheduling delays**, not "GPU boost" tricks.

## Suggested order

1. **Run the Benchmark** with the label "Before" (Extras → Benchmark).
2. **Quick Optimize → Gaming**, then also tick the **Privacy** preset's telemetry items if you want them. Review the list and press **Apply**.
3. **Startup page**: switch off everything you don't need at boot (launchers, updaters, RGB tools). On a 4-thread CPU, every resident app costs something.
4. **Restart**, run the Benchmark again with the label "After", and play your usual games. Keep what helps.

## Recommended tweaks and why

| Tweak | Why it fits this PC |
|---|---|
| Hardware-accelerated GPU scheduling | Supported by the GTX 1650 Super (driver 451.48+). Moves frame scheduling off your 4 CPU threads. Measure it: most games gain a little, a few lose. |
| Disable CPU core parking | With only 4 cores, a parked core waking up mid-frame is a visible hitch. |
| Foreground boost (Win32PrioritySeparation 0x26) | Gives the game window longer, boosted time slices over background tasks. |
| MMCSS game priority + remove network throttling | Less CPU reserved for background multimedia work (10% instead of 20%). |
| Disable Game DVR | Background recording uses NVENC and disk. Use ShadowPlay or OBS on demand instead. |
| Ultimate Performance plan + USB suspend off + PCIe ASPM off | You're on a desktop, so there's no battery to save, and power-state transitions add latency. |
| Aggressive turbo boost | Helps the i3 hold its 4.2 GHz turbo. The stock cooler handles a 65 W chip fine, but check temperatures stay under ~85 °C in games. |
| Disable background apps, tips and telemetry | Small, but it's CPU time your 4 threads don't have spare. |
| Mouse acceleration off (MarkC 1:1) | Consistent aim. Sign out and back in after applying. |

## Things frtune deliberately does **not** recommend for you

| Tweak | Why not |
|---|---|
| Disable memory compression | With 16 GB it costs almost nothing and helps when a game plus browser approach the limit. Leave it on. |
| Disable the pagefile / fixed small pagefile | Keep it system-managed. Some games crash without a pagefile even with free RAM. |
| Disable Spectre/Meltdown mitigations | On recent Intel CPUs the mitigations cost little (some fixes are in hardware), so the gain is small and the risk (browser-based attacks) is real. |
| Disable VBS / Memory Integrity | On Windows 11 it can cost a few % on a 4-thread CPU, but it blocks a whole class of kernel malware, and some anti-cheats and work/school policies expect it. It's on the Advanced page if you decide you want it. |
| Disable SysMain | Only offered when your system drive is an SSD. On an HDD, SysMain noticeably helps load times. |
| GPU preference per game | Your CPU has **no integrated GPU** (the "F" suffix), so there's only one GPU to choose. CPU priority per game still works. |

## Outside frtune (worth doing)

- **RAM in dual channel**: 16 GB should be **2 × 8 GB**, in slots A2/B2 (check your motherboard manual). A single 16 GB stick costs a lot of FPS on this CPU. The Hardware page tells you how many sticks are installed.
- **XMP / memory speed**: Intel officially limits the i3-9100F to DDR4-2400, and B360/B365/H310 boards enforce that. Faster sticks run at 2400 on those boards, which is expected.
- **NVIDIA Control Panel → Manage 3D settings**: Low Latency Mode **On** (or Ultra in competitive games), Power management **Prefer maximum performance** (per game), Shader cache size **Driver default** or **10 GB**. frtune doesn't change driver settings.
- **4 GB VRAM**: in new games, keep texture quality at Medium/High rather than Ultra. Running out of VRAM causes stutter that no Windows tweak can fix.
- **Driver**: a clean install of the current Game Ready driver. After a driver update, clear the **NVIDIA shader cache** in the Cleaner if games stutter.
