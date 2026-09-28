using System.Numerics;
using ImGuiNET;
using TabletAb.Core.Protocol;
using TabletAb.Input;

namespace TabletAb.App.UI;

/// <summary>
/// The A/B acquisition control panel. Every control pushes its command
/// immediately via the supplied send delegate (which is a no-op while
/// disconnected; "Push all" re-sends the whole set). The <see cref="ICommandBuilder"/>
/// selects the v6 or legacy v5 opcodes to match the connected firmware.
/// </summary>
public sealed class AcquisitionPanel
{
    private static readonly ProtocolChoice[] FrequencyChoices = BuildFrequencyChoices();

    /// <summary>Set when the user clicks "Push all settings".</summary>
    public bool PushAllRequested { get; private set; }

    public void DrawContent(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands, bool connected)
    {
        PushAllRequested = false;

        ImGui.TextDisabled(connected
            ? $"changes apply immediately ({commands.Name})"
            : "disconnected — edits are sent when a device connects");

        bool freeze = s.FreezeDisplay;
        if (ImGui.Checkbox("Freeze display##freeze", ref freeze))
            s.FreezeDisplay = freeze;

        ImGui.SameLine();
        if (ImGui.Button("Push all settings"))
            PushAllRequested = true;

        ImGui.Separator();

        DrawFrequencyPacing(s, send, commands);
        DrawDrive(s, send, commands);
        DrawAdc(s, send, commands);
        DrawWindow(s, send, commands);
        DrawTracking(s, send, commands);
        DrawEstimator(s, send, commands);
    }

    private static void DrawFrequencyPacing(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("Frequency & pacing", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        bool continuous = s.UseFrequencyArr;
        using (new DisabledIf(!s.HardwareTiming))
        {
            if (ImGui.Checkbox("Continuous (raw ARR, hardware backend)", ref continuous))
            {
                s.UseFrequencyArr = continuous;
                if (continuous)
                    commands.SetFrequencyArr(send, s.FrequencyArr);
                else
                    commands.SetFrequency(send, s.FrequencyIndex); // restore index table
            }
        }

        if (continuous)
        {
            using (new DisabledIf(!s.HardwareTiming))
            {
                IntSlider("ARR (72 MHz counts)##arr", s.FrequencyArr, 100, 400, v =>
                {
                    s.FrequencyArr = v;
                    commands.SetFrequencyArr(send, v);
                });
            }

            if (!s.HardwareTiming)
                ImGui.TextDisabled("software backend has no continuous frequency");
        }
        else
        {
            ChoiceCombo("Frequency index##freq", FrequencyChoices, s.FrequencyIndex, v =>
            {
                s.FrequencyIndex = v;
                commands.SetFrequency(send, v);
            });
        }

        ChoiceCombo("Pacing##pacing", ProtocolCatalog.PacingModes, s.Pacing, v =>
        {
            s.Pacing = v;
            commands.SetPacing(send, v);
        });
    }

    private static void DrawDrive(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("Drive and settles", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ChoiceCombo("Timing backend##timing",
            [new(0, "software (NOP sled)"), new(1, "hardware timers")],
            s.HardwareTiming ? 1 : 0,
            v =>
            {
                s.HardwareTiming = v == 1;
                commands.SetBackend(send, s.HardwareTiming ? 1 : 0);

                // The raw ARR is hardware-only; switching to software must
                // restore the frequency index table.
                if (!s.HardwareTiming && s.UseFrequencyArr)
                {
                    s.UseFrequencyArr = false;
                    commands.SetFrequency(send, s.FrequencyIndex);
                }
            });

        IntSlider("Burst periods##burst", s.Burst, 6, 29, v =>
        {
            s.Burst = v;
            commands.SetBurst(send, v);
        });

        ChoiceCombo("Ramp mitigation##ramp", ProtocolCatalog.RampModes, s.RampMode, v =>
        {
            s.RampMode = v;
            commands.SetRamp(send, v, s.PrimeBurst, s.PrimeRepeats, s.RampSlope, s.ReverseRadius);
        });

        if (s.RampMode is 2 or 3)
        {
            IntInput("Prime periods##pb", s.PrimeBurst, 6, 255, v =>
            {
                s.PrimeBurst = v;
                commands.SetRamp(send, s.RampMode, s.PrimeBurst, s.PrimeRepeats, s.RampSlope, s.ReverseRadius);
            });

            if (s.RampMode == 2)
            {
                IntInput("Prime repeats##pr", s.PrimeRepeats, 1, 32, v =>
                {
                    s.PrimeRepeats = v;
                    commands.SetRamp(send, s.RampMode, s.PrimeBurst, s.PrimeRepeats, s.RampSlope, s.ReverseRadius);
                });
            }
        }

        if (s.RampMode == 5)
        {
            IntSlider("Slope (per-mille/coil)##slope", s.RampSlope, -1000, 1000, v =>
            {
                s.RampSlope = v;
                commands.SetRamp(send, s.RampMode, s.PrimeBurst, s.PrimeRepeats, s.RampSlope, s.ReverseRadius);
            });
        }

        if (s.RampMode == 6)
        {
            IntInput("Reverse radius##revr", s.ReverseRadius, 1, 8, v =>
            {
                s.ReverseRadius = v;
                commands.SetRamp(send, s.RampMode, s.PrimeBurst, s.PrimeRepeats, s.RampSlope, s.ReverseRadius);
            });
        }

        ImGui.Separator();

        void SendSettle() => commands.SetSettle(send, s.SettleA, s.SettleB, s.SettleC, s.SettleD);
        void SendSettleCycles() => commands.SetSettleCycles(send, s.SettleCycA, s.SettleCycB, s.SettleCycC, s.SettleCycD);

        bool useCycle = s.UseCycleSettle && commands.SupportsCycleSettle;
        if (commands.SupportsCycleSettle)
        {
            if (ImGui.Checkbox("Cycle-accurate settle (DWT cycles, 72 = 1 µs)##cyclesettle", ref useCycle))
            {
                s.UseCycleSettle = useCycle;
                if (useCycle) SendSettleCycles(); else SendSettle();
            }
            if (!s.UseCycleSettle)
                ImGui.SameLine();
        }

        if (useCycle)
        {
            ImGui.Text($"Settle sites A / B / C / D (cycles; {s.SettleCycA / 72.0:F2} / {s.SettleCycB / 72.0:F2} / " +
                       $"{s.SettleCycC / 72.0:F2} / {s.SettleCycD / 72.0:F2} µs)");
            IntInput("A##setca", s.SettleCycA, 0, 65535, v => { s.SettleCycA = v; SendSettleCycles(); });
            ImGui.SameLine();
            IntInput("B##setcb", s.SettleCycB, 0, 65535, v => { s.SettleCycB = v; SendSettleCycles(); });
            ImGui.SameLine();
            IntInput("C##setcc", s.SettleCycC, 0, 65535, v => { s.SettleCycC = v; SendSettleCycles(); });
            ImGui.SameLine();
            IntInput("D##setcd", s.SettleCycD, 0, 65535, v => { s.SettleCycD = v; SendSettleCycles(); });
        }
        else
        {
            ImGui.Text("Settle sites A / B / C / D (µs)");
            IntInput("A##seta", s.SettleA, 0, 255, v => { s.SettleA = v; SendSettle(); });
            ImGui.SameLine();
            IntInput("B##setb", s.SettleB, 0, 255, v => { s.SettleB = v; SendSettle(); });
            ImGui.SameLine();
            IntInput("C##setc", s.SettleC, 0, 255, v => { s.SettleC = v; SendSettle(); });
            ImGui.SameLine();
            IntInput("D##setd", s.SettleD, 0, 255, v => { s.SettleD = v; SendSettle(); });
        }

        IntInput("Repeat coil (0 = off)##repeat", s.RepeatCoil, 0, ProtocolConstants.Nx, v =>
        {
            s.RepeatCoil = v;
            commands.RepeatCoil(send, v);
        });
    }

    private static void DrawAdc(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("ADC", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        IntSlider("Samples per channel##adcn", s.AdcSamples, 1, 7, v =>
        {
            s.AdcSamples = v;
            commands.SetAdc(send, s.AdcSamples, s.AdcClock);
        });

        ChoiceCombo("ADC clock##adcclk", ProtocolCatalog.AdcClockLabels, s.AdcClock, v =>
        {
            s.AdcClock = v;
            commands.SetAdc(send, s.AdcSamples, s.AdcClock);
        });

        BoolCheck("Coil recovery (site D)##recovery", s.CoilRecovery, v =>
        {
            s.CoilRecovery = v;
            commands.SetRecovery(send, v);
        });
    }

    private static void DrawWindow(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("Window & re-acquire", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ChoiceCombo("Window coils X##winx", ProtocolCatalog.WindowCoils, s.WindowCoilsX, v =>
        {
            s.WindowCoilsX = v;
            commands.SetWindow(send, s.WindowCoilsX, s.WindowCoilsY);
        });

        ChoiceCombo("Window coils Y##winy", ProtocolCatalog.WindowCoils, s.WindowCoilsY, v =>
        {
            s.WindowCoilsY = v;
            commands.SetWindow(send, s.WindowCoilsX, s.WindowCoilsY);
        });

        void SendReacquire()
            => commands.SetReacquire(send, s.ReacquireThreshold, s.CoarseStride, s.ReacquirePeriodMs);

        IntInput("Re-acquire threshold##reacq", s.ReacquireThreshold, 0, 65535, v =>
        {
            s.ReacquireThreshold = v;
            SendReacquire();
        });

        IntInput("Re-acquire period (ms)##reacqms", s.ReacquirePeriodMs, 0, 65535, v =>
        {
            s.ReacquirePeriodMs = v;
            SendReacquire();
        });

        ChoiceCombo("Re-acquire scan##coarse", ProtocolCatalog.CoarseStrides, s.CoarseStride, v =>
        {
            s.CoarseStride = v;
            SendReacquire();
        });
    }

    private static void DrawTracking(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("Tracking", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ChoiceCombo("Window recentre##recentre", ProtocolCatalog.RecentreModes, s.RecentreMode, v =>
        {
            s.RecentreMode = v;
            SendRecentre(s, send, commands);
        });

        using (new DisabledIf(s.RecentreMode != 2))
        {
            IntInput("RC hysteresis (%)##rchyst", s.RecentreHyst, 1, 255, v =>
            {
                s.RecentreHyst = v;
                SendRecentre(s, send, commands);
            });
        }

        using (new DisabledIf(s.RecentreMode != 3))
        {
            IntInput("RC deadband (coils)##rcdead", s.RecentreDeadband, 1, 255, v =>
            {
                s.RecentreDeadband = v;
                SendRecentre(s, send, commands);
            });
        }

        using (new DisabledIf(s.RecentreMode is not (2 or 3)))
        {
            IntInput("RC persist##rcpersist", s.RecentrePersist, 1, 255, v =>
            {
                s.RecentrePersist = v;
                SendRecentre(s, send, commands);
            });
            IntInput("RC step (0 = snap)##rcstep", s.RecentreStep, 0, 255, v =>
            {
                s.RecentreStep = v;
                SendRecentre(s, send, commands);
            });
        }

        ChoiceCombo("Scan order##scanorder", ProtocolCatalog.ScanOrders, s.ScanOrder, v =>
        {
            s.ScanOrder = v;
            commands.SetScanOrder(send, v);
        });

        IntSlider("Warm-up reads##warmup", s.Warmup, 0, 8, v =>
        {
            s.Warmup = v;
            commands.SetWarmup(send, v);
        });

        IntInput("Flat-hold tolerance##flat", s.FlatHoldTolerance, 0, 65535, v =>
        {
            s.FlatHoldTolerance = v;
            commands.SetFlatTolerance(send, v);
        });
    }

    private static void DrawEstimator(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
    {
        if (!ImGui.CollapsingHeader("Estimator", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ChoiceCombo("Sub-pixel estimator##est", ProtocolCatalog.Estimators, s.Estimator, v =>
        {
            s.Estimator = v;
            commands.SetEstimator(send, v);
        });

        if (s.Estimator == 0 || s.Estimator == 2)
        {
            IntInput("Noise base (centroid/gaomon)##cent", s.CentroidBase, 0, 65535, v =>
            {
                s.CentroidBase = v;
                commands.SetCentroidBase(send, v);
            });
        }

        if (s.Estimator == 3)
        {
            void SendLog()
            {
                commands.SetLogGaussian(send, 0, s.LogBaseline, s.LogA1x, s.LogA3x);
                commands.SetLogGaussian(send, 1, s.LogBaseline, s.LogA1y, s.LogA3y);
            }

            IntInput("LG baseline##lgb", s.LogBaseline, 0, 65535, v =>
            {
                s.LogBaseline = v;
                SendLog();
            });

            IntInput("LG a1 X##lga1x", s.LogA1x, -32768, 32767, v => { s.LogA1x = v; SendLog(); });
            ImGui.SameLine();
            IntInput("LG a1 Y##lga1y", s.LogA1y, -32768, 32767, v => { s.LogA1y = v; SendLog(); });

            IntInput("LG a3 X##lga3x", s.LogA3x, -32768, 32767, v => { s.LogA3x = v; SendLog(); });
            ImGui.SameLine();
            IntInput("LG a3 Y##lga3y", s.LogA3y, -32768, 32767, v => { s.LogA3y = v; SendLog(); });
        }
    }

    private static void SendRecentre(AcquisitionSettings s, Action<byte[]> send, ICommandBuilder commands)
        => commands.SetRecenter(send,
            s.RecentreMode,
            s.RecentreHyst,
            s.RecentrePersist,
            s.RecentreStep,
            s.RecentreDeadband);

    // --- widgets ---

    private static void IntSlider(string label, int current, int min, int max, Action<int> changed)
    {
        int value = current;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt(label, ref value, min, max))
            changed(value);
    }

    private static void IntInput(string label, int current, int min, int max, Action<int> changed)
    {
        int value = current;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt(label, ref value))
            changed(Math.Clamp(value, min, max));
    }

    private static void BoolCheck(string label, bool current, Action<bool> changed)
    {
        bool value = current;
        if (ImGui.Checkbox(label, ref value))
            changed(value);
    }

    private static void ChoiceCombo(string label, ProtocolChoice[] options, int current, Action<int> changed)
    {
        int index = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].Value == current)
            {
                index = i;
                break;
            }
        }

        string[] labels = Array.ConvertAll(options, o => o.Label);
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo(label, ref index, labels, labels.Length))
            changed(options[index].Value);
    }

    private static ProtocolChoice[] BuildFrequencyChoices()
    {
        var choices = new ProtocolChoice[12];
        for (int i = 0; i < 12; i++)
            choices[i] = new ProtocolChoice(i + 1, $"index {i + 1}");
        return choices;
    }

    /// <summary>Scoped <c>BeginDisabled</c>/<c>EndDisabled</c>.</summary>
    private readonly struct DisabledIf : IDisposable
    {
        private readonly bool _disabled;

        public DisabledIf(bool disabled)
        {
            _disabled = disabled;
            if (disabled)
                ImGui.BeginDisabled();
        }

        public void Dispose()
        {
            if (_disabled)
                ImGui.EndDisabled();
        }
    }
}
