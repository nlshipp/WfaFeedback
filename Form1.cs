using SharpDX.DirectInput;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WfaFeedback
{
    public enum DI
    {
        Infinite = -1,
        Seconds = 1000000,
        NominalMax = 10000,
        Degrees = 100
    }

    public partial class Form1 : Form
    {
        /// <summary>
        /// This structure will contain information about an effect,
        /// its Effect structure, and the DirectInputEffect object.
        /// </summary>
        private class EffectDescription
        {
            public EffectInfo info;
            public Effect effect;
            public EffectParameters parameters;

            public override string ToString()
            {
                return info.Name;
            }

            public EffectType Type
            {
                get
                {
                    return info.Type & EffectType.Hardware;  // Hardware == 0xFFu
                }
                private set { }
            }
        }

        private Joystick applicationDevice; //DirectInput device object.
        private EffectDescription effectSelected; //The currently selected effect.

        private int[] axis; //Holds the FF axes offsets.
        private bool isChanging; // Flag that is set when that app is changing control values.

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Initialize the DirectInput objects
            if (!InitializeDirectInput())
            {
                Close();
            }
            else
            {
                try
                {
                    applicationDevice.Acquire();
                }
                catch { }
            }
        }

        private void Form1_Enter(object sender, EventArgs e)
        {
            // Reclaim FFB joystick
            if (applicationDevice != null)
            {
                try
                {
                    applicationDevice.Acquire();
                }
                catch { }
            }
        }


        private void lstEffects_SelectedIndexChanged(object sender, EventArgs e)
        {
            EffectDescription description;

            if (null != effectSelected)
            {
                try
                {
                    effectSelected.effect.Unload();
                }
                catch { }
            }

            description = (EffectDescription)lstEffects.Items[lstEffects.SelectedIndex];
            effectSelected = description;
            UpdateVisibility();

            try
            {
                effectSelected.effect.Start(EffectPlayFlags.None);
            }
            catch (SharpDX.SharpDXException ex) { MessageBox.Show(ex.Message); }
        }

        
        /// <summary>
        /// Changes the parameters of an effect.
        /// </summary>
        private EffectParameters ChangeParameter()
        {
            EffectParameters eff = GetEffectParameters();
            int flags = (int)EffectParameterFlags.Start;
            int i = 0;

            switch (effectSelected.Type)
            {
                case EffectType.ConstantForce:
                    eff.Parameters.As<ConstantForce>().Magnitude = ConstantForceMagnitude.Value;
                    flags = flags | (int)EffectParameterFlags.TypeSpecificParameters;
                    break;
                case EffectType.RampForce:
                    eff.Parameters.As<RampForce>().Start = RangeStart.Value;
                    eff.Parameters.As<RampForce>().End = RangeEnd.Value;
                    flags = (int)EffectParameterFlags.TypeSpecificParameters;
                    if ((int)DI.Infinite == eff.Duration)
                    {
                        // Default to a 2 second ramp effect
                        // if DI.Infinite is passed in.
                        // DI.Infinite is invalid for ramp forces.
                        eff.Duration = 2 * (int)DI.Seconds;
                        flags = flags | (int)EffectParameterFlags.Duration;
                    }
                    flags = flags | (int)EffectParameterFlags.Start;
                    break;
                case EffectType.Periodic:

                    eff.Parameters.As<PeriodicForce>().Magnitude = PeriodicMagnitude.Value;
                    eff.Parameters.As<PeriodicForce>().Offset = PeriodicOffset.Value;
                    eff.Parameters.As<PeriodicForce>().Period = PeriodicPeriod.Value;
                    eff.Parameters.As<PeriodicForce>().Phase = PeriodicPhase.Value;

                    flags = flags | (int)EffectParameterFlags.TypeSpecificParameters;
                    break;
                case EffectType.Condition:
                    if (ConditionalAxis1.Checked == true)
                        i = 0;
                    else
                        i = 1;

                    eff.Parameters.As<ConditionSet>().Conditions[i].DeadBand = ConditionalDeadBand.Value;
                    eff.Parameters.As<ConditionSet>().Conditions[i].NegativeCoefficient = ConditionalNegativeCoeffcient.Value;
                    eff.Parameters.As<ConditionSet>().Conditions[i].NegativeSaturation = ConditionalNegativeSaturation.Value;
                    eff.Parameters.As<ConditionSet>().Conditions[i].Offset = ConditionalOffset.Value;
                    eff.Parameters.As<ConditionSet>().Conditions[i].PositiveCoefficient = ConditionalPositiveCoefficient.Value;
                    eff.Parameters.As<ConditionSet>().Conditions[i].PositiveSaturation = ConditionalPositiveSaturation.Value;

                    flags = flags | (int)EffectParameterFlags.TypeSpecificParameters;
                    break;
            }

            // Some feedback drivers will fail when setting parameters that aren't supported by
            // an effect. DirectInput will will in turn pass back the driver error to the application.
            // Since these are hardware specific error messages that can't be handled individually, 
            // the app will ignore any failures returned to SetParameters().
            try
            {
                effectSelected.parameters = eff;
                effectSelected.effect.SetParameters(eff, EffectParameterFlags.TypeSpecificParameters);
            }
            catch
            {
                eff = GetEffectParameters();
            }

            return eff;
        }


        /// <summary>
        /// Changes the direction of an effect.
        /// </summary>
        private void ChangeDirection(int[] direction)
        {
            EffectParameters eff;

            eff = effectSelected.parameters;
            eff.Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets;
            eff.Directions = direction;

            // Some feedback drivers will fail when setting parameters that aren't supported by
            // an effect. DirectInput will will in turn pass back the driver error to the application.
            // Since these are hardware specific error messages that can't be handled individually, 
            // the app will ignore any failures returned to SetParameters().
            try
            {
                effectSelected.effect.SetParameters(eff, EffectParameterFlags.Direction | EffectParameterFlags.Start);
            }
//            catch (InputException) { }
            catch (SharpDX.SharpDXException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        /// <summary>
        /// Changes the envelope of an effect.
        /// </summary>
        private EffectParameters ChangeEnvelope()
        {
            EffectParameters eff = effectSelected.parameters;

            if (!isChanging)
            {
                if (!chkUseEnvelope.Checked)
                {
                    eff.Envelope = null;
                }
                else
                {
                    eff.Envelope = new Envelope();
                    eff.Envelope.AttackLevel = EnvelopeAttackLevel.Value;
                    eff.Envelope.AttackTime = EnvelopeAttackTime.Value;
                    eff.Envelope.FadeLevel = EnvelopeFadeLevel.Value;
                    eff.Envelope.FadeTime = EnvelopeFadeTime.Value;
                }

                // Some feedback drivers will fail when setting parameters that aren't supported by
                // an effect. DirectInput will will in turn pass back the driver error to the application.
                // Since these are hardware specific error messages that can't be handled individually, 
                // the app will ignore any failures returned to SetParameters().
                try
                {
                    effectSelected.effect.SetParameters(eff, EffectParameterFlags.Envelope | EffectParameterFlags.Start);
                }
//                catch (InputException) { }
                catch (SharpDX.SharpDXException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            return eff;
        }


        /// <summary>
        /// Fills in an Effect structure with effect information.
        /// </summary>
        private EffectParameters GetEffectParameters()
        {
            EffectParameters eff = effectSelected.parameters;
            eff.Flags = EffectFlags.ObjectIds | EffectFlags.Cartesian;

            // If this is a condition effect, see if the Effect.Condition member
            // array length that was returned from GetParameters() has enough elements 
            // to cover 2 axes if this is a two axis device. In most cases, conditional 
            // effects will return 1 Condition element that can be applied across 
            // all force-feedback axes.
            if ((eff.Parameters != null) && (eff.Parameters.As<ConditionSet>() != null))
            {
                ConditionSet cs = eff.Parameters.As<ConditionSet>();

                if ((rbConditionalAxis2.Enabled) && (cs.Conditions.Length < 2))
                {
                    // Resize the array for two axes.
                    Condition[] temp = new Condition[2];
                    cs.Conditions.CopyTo(temp, 0);
                    cs.Conditions = temp;
                    // Copy the conditional effect info from one struct to the other.
                    cs.Conditions[1] = cs.Conditions[0];
                }
            }
            return eff;
        }

        
        /// <summary>
        /// Updates the visibility of each
        /// effect parameters group box, as well
        /// as general parameter, envelope, and
        /// direction group boxes.
        /// </summary>
        private void UpdateVisibility()
        {
            isChanging = true;

            if (null == effectSelected)
                return;

            EffectDescription description = (EffectDescription)lstEffects.Items[lstEffects.SelectedIndex];
            EffectParameters eff = GetEffectParameters();

            GroupBox Current = new GroupBox();

            // Check to see what type of effect this is,
            // and then change the visibilty of the
            // group boxes accordingly.
            switch (effectSelected.Type)
            {
                case EffectType.Condition:
                    Current = GroupConditionalForce;
                    UpdateConditionalGroupBox(eff);
                    break;
                case EffectType.ConstantForce:
                    Current = GroupConstantForce;
                    UpdateConstantGroupBox(eff);
                    break;
                case EffectType.Periodic:
                    Current = GroupPeriodForce;
                    UpdatePeriodicGroupBox(eff);
                    break;
                case EffectType.RampForce:
                    Current = GroupRampForce;
                    UpdateRampGroupBox(eff);
                    break;
            }

            foreach (GroupBox target in gbTypeContainer.Controls)
            {
                if (Current == target)
                    target.Visible = true;
                else
                    target.Visible = false;
            }

            // Check the effect info and update the controls
            // to show whether the parameters are supported.
            if (0 == (description.info.StaticParameters & EffectParameterFlags.Direction))
                DirectionGroupBox.Enabled = false;
            else
                DirectionGroupBox.Enabled = true;

            if (0 == (description.info.StaticParameters & EffectParameterFlags.Duration))
                GeneralDurationLabel.Enabled = GeneralDuration.Enabled = false;
            else
                GeneralDurationLabel.Enabled = GeneralDuration.Enabled = true;

            if (0 == (description.info.StaticParameters & EffectParameterFlags.Gain))
                GeneralGainLabel.Enabled = GeneralGain.Enabled = false;
            else
                GeneralGainLabel.Enabled = GeneralGain.Enabled = true;

            if (0 == (description.info.StaticParameters & EffectParameterFlags.SamplePeriod))
                GeneralPeriodLabel.Enabled = GeneralPeriod.Enabled = false;
            else
                GeneralPeriodLabel.Enabled = GeneralPeriod.Enabled = true;

            // Update the general parameter
            // and envelope controls.
            UpdateGeneralParamsGroupBox(eff);

            // Reflect support for envelopes on this effect.
            UpdateEnvParamsGroupBox(eff);
            EnvelopeGroupBox.Enabled = ((description.info.StaticParameters & EffectParameterFlags.Envelope) != 0) ? true : false;

            // Update direction radio buttons.
            if (1 == axis.Length)
            {
                if (2 == eff.Directions[0])
                    East.Checked = true;
                else
                    West.Checked = true;
            }
            else if (2 >= axis.Length)
            {
                if (2 == eff.Directions[0] && 0 == eff.Directions[1])
                    East.Checked = true;
                else if (-2 == eff.Directions[0] && 0 == eff.Directions[1])
                    West.Checked = true;
                else if (0 == eff.Directions[0] && -2 == eff.Directions[1])
                    North.Checked = true;
                else if (0 == eff.Directions[0] && 2 == eff.Directions[1])
                    South.Checked = true;
                else if (1 == eff.Directions[0] && -1 == eff.Directions[1])
                    NorthEast.Checked = true;
                else if (1 == eff.Directions[0] && 1 == eff.Directions[1])
                    SouthEast.Checked = true;
                else if (-1 == eff.Directions[0] && 1 == eff.Directions[1])
                    SouthWest.Checked = true;
                else if (-1 == eff.Directions[0] && -1 == eff.Directions[1])
                    NorthWest.Checked = true;
                else if (0 == eff.Directions[0] && 0 == eff.Directions[1])
                    East.Checked = true;
            }

            isChanging = false;
        }


        /// <summary>
        /// Updates the general parameters controls and labels.
        /// </summary>
        private void UpdateGeneralParamsGroupBox(EffectParameters eff)
        {
            if (((eff.Duration / (int)DI.Seconds) > GeneralDuration.Maximum) || (eff.Duration < 0))
                GeneralDuration.Value = GeneralDuration.Maximum;
            else
                GeneralDuration.Value = eff.Duration / (int)DI.Seconds;

            if (eff.Gain > GeneralGain.Maximum)
                GeneralGain.Value = GeneralGain.Maximum;
            else
                GeneralGain.Value = eff.Gain;

            if (eff.SamplePeriod > GeneralPeriod.Maximum)
                GeneralPeriod.Value = GeneralPeriod.Maximum;
            else
                GeneralPeriod.Value = eff.SamplePeriod;

            if ((int)DI.Infinite == eff.Duration)
                GeneralDurationLabel.Text = "Effect Duration: Infinite";
            else
                GeneralDurationLabel.Text = "Effect Duration: " + (eff.Duration / (int)DI.Seconds) + " seconds";

            GeneralGainLabel.Text = "Effect Gain: " + GeneralGain.Value;

            if (0 == eff.SamplePeriod)
                GeneralPeriodLabel.Text = "Sample Rate: Default";
            else
                GeneralPeriodLabel.Text = "Sample Period: " + eff.SamplePeriod;

        }


        /// <summary>
        /// Updates the controls and labels for constant force effects.
        /// </summary>
        private void UpdateConstantGroupBox(EffectParameters eff)
        {
            ConstantForceMagnitude.Value = eff.Parameters.As<ConstantForce>().Magnitude;
            Magnitude.Text = "Constant Force Magnitude: " + ConstantForceMagnitude.Value;
        }


        /// <summary>
        /// Updates the controls and labels for ramp effects.
        /// </summary>
        private void UpdateRampGroupBox(EffectParameters eff)
        {
            RangeStart.Value = eff.Parameters.As<RampForce>().Start;
            RangeEnd.Value = eff.Parameters.As<RampForce>().End;
            RangeStartLabel.Text = "Range Start: " + RangeStart.Value;
            RangeEndLabel.Text = "Range End: " + RangeEnd.Value;
        }


        /// <summary>
        /// Updates the controls and labels for periodic effects.
        /// </summary>
        private void UpdatePeriodicGroupBox(EffectParameters eff)
        {
            if (eff.Parameters.As<PeriodicForce>().Magnitude < PeriodicMagnitude.Maximum)
                PeriodicMagnitude.Value = eff.Parameters.As<PeriodicForce>().Magnitude;
            else
                PeriodicMagnitude.Value = PeriodicMagnitude.Maximum;

            if (eff.Parameters.As<PeriodicForce>().Offset < PeriodicOffset.Maximum)
                PeriodicOffset.Value = eff.Parameters.As<PeriodicForce>().Offset;
            else
                PeriodicOffset.Value = PeriodicOffset.Maximum;

            if (eff.Parameters.As<PeriodicForce>().Period < PeriodicPeriod.Maximum)
                PeriodicPeriod.Value = eff.Parameters.As<PeriodicForce>().Period;
            else
                PeriodicPeriod.Value = PeriodicPeriod.Maximum;

            if (eff.Parameters.As<PeriodicForce>().Phase < PeriodicPhase.Maximum)
                PeriodicPhase.Value = eff.Parameters.As<PeriodicForce>().Phase;
            else
                PeriodicPhase.Value = PeriodicPhase.Maximum;

            PeriodicMagnitudeLabel.Text = "Magnitude: " + PeriodicMagnitude.Value;
            PeriodicOffsetLabel.Text = "Offset: " + PeriodicOffset.Value;
            PeriodicPeriodLabel.Text = "Period: " + PeriodicPeriod.Value;
            PeriodicPhaseLabel.Text = "Phase: " + PeriodicPhase.Value;
        }


        /// <summary>
        /// Updates the controls in the Conditional group box.
        /// </summary>
        private void UpdateConditionalGroupBox(EffectParameters eff)
        {
            int i;

            if (true == ConditionalAxis1.Checked)
                i = 0;
            else
                i = 1;

            ConditionalDeadBand.Value = eff.Parameters.As<ConditionSet>().Conditions[i].DeadBand;
            ConditionalOffset.Value = eff.Parameters.As<ConditionSet>().Conditions[i].Offset;
            ConditionalNegativeCoeffcient.Value = eff.Parameters.As<ConditionSet>().Conditions[i].NegativeCoefficient;
            ConditionalNegativeSaturation.Value = eff.Parameters.As<ConditionSet>().Conditions[i].NegativeSaturation;
            ConditionalPositiveCoefficient.Value = eff.Parameters.As<ConditionSet>().Conditions[i].PositiveCoefficient;
            ConditionalPositiveSaturation.Value = eff.Parameters.As<ConditionSet>().Conditions[i].PositiveSaturation;

            ConditionalDeadBandLabel.Text = "Dead Band: " + ConditionalDeadBand.Value;
            ConditionalOffsetLabel.Text = "Offset: " + ConditionalOffset.Value;
            ConditionalNegativeCoeffcientLabel.Text = "Negative Coefficient: " + ConditionalNegativeCoeffcient.Value;
            ConditionalNegativeSaturationLabel.Text = "Negative Saturation: " + ConditionalNegativeSaturation.Value;
            ConditionalPositiveCoefficientLabel.Text = "Positive Coefficient: " + ConditionalPositiveCoefficient.Value;
            ConditionalPositiveSaturationLabel.Text = "Positive Saturation: " + ConditionalPositiveSaturation.Value;

            // change visibility with respect to EffectType flags
            ConditionalNegativeCoeffcient.Enabled = effectSelected.info.Type.HasFlag(EffectType.TwoCoefficients);
            ConditionalNegativeCoeffcientLabel.Enabled = effectSelected.info.Type.HasFlag(EffectType.TwoCoefficients);
            ConditionalNegativeSaturation.Enabled = effectSelected.info.Type.HasFlag(EffectType.TwoSaturations);
            ConditionalNegativeSaturationLabel.Enabled = effectSelected.info.Type.HasFlag(EffectType.TwoSaturations);
            ConditionalDeadBand.Enabled = effectSelected.info.Type.HasFlag(EffectType.DeadBand);
            ConditionalDeadBandLabel.Enabled = effectSelected.info.Type.HasFlag(EffectType.DeadBand);
        }


        /// <summary>
        /// Updates the env params group box
        /// </summary>
        private void UpdateEnvParamsGroupBox(EffectParameters eff)
        {
            if (eff.Envelope != null)
            { 
                chkUseEnvelope.Checked = true;

                if (eff.Envelope.AttackLevel > EnvelopeAttackLevel.Maximum)
                    EnvelopeAttackLevel.Value = EnvelopeAttackLevel.Maximum;
                else
                    EnvelopeAttackLevel.Value = eff.Envelope.AttackLevel;

                if (eff.Envelope.AttackTime > EnvelopeAttackTime.Maximum)
                    EnvelopeAttackTime.Value = EnvelopeAttackTime.Maximum;
                else
                    EnvelopeAttackTime.Value = eff.Envelope.AttackTime;

                if (eff.Envelope.FadeLevel > EnvelopeFadeLevel.Maximum)
                    EnvelopeFadeLevel.Value = EnvelopeFadeLevel.Maximum;
                else
                    EnvelopeFadeLevel.Value = eff.Envelope.FadeLevel;

                if (eff.Envelope.FadeTime > EnvelopeFadeTime.Maximum)
                    EnvelopeFadeTime.Value = EnvelopeFadeTime.Maximum;
                else
                    EnvelopeFadeTime.Value = eff.Envelope.FadeTime;

                EnvelopeAttackLevelLabel.Text = "Attack Level: " + eff.Envelope.AttackLevel;
                EnvelopeAttackTimeLabel.Text = "Attack Time: " + eff.Envelope.AttackTime / 1000;
                EnvelopeFadeLevelLabel.Text = "Fade Level: " + eff.Envelope.FadeLevel;
                EnvelopeFadeTimeLabel.Text = "Fade Time: " + eff.Envelope.FadeTime / 1000;
            }
            else
            {
                chkUseEnvelope.Checked = false;
            }
        }


        /// <summary>
        /// Handles changing the axis on a conditional effect.
        /// </summary>
        private void ConditionalAxisChanged(object sender, System.EventArgs e)
        {
            EffectParameters eff = GetEffectParameters();
            UpdateConditionalGroupBox(eff);

        }


        /// <summary>
        /// Handles the trackbar scroll events for constant effects.
        /// </summary>
        private void ConstantForceMagnitudeScroll(object sender, System.EventArgs e)
        {
            EffectParameters eff = ChangeParameter();
            UpdateConstantGroupBox(eff);
        }


        /// <summary>
        /// Handles the trackbar scroll events for ramp effects.
        /// </summary>
        private void RangeScroll(object sender, System.EventArgs e)
        {
            EffectParameters eff = ChangeParameter();
            UpdateRampGroupBox(eff);

        }


        /// <summary>
        /// Handles the trackbar scroll events for periodic effects.
        /// </summary>
        private void PeriodicScroll(object sender, System.EventArgs e)
        {
            EffectParameters eff = ChangeParameter();
            UpdatePeriodicGroupBox(eff);
        }


        /// <summary>
        /// Handles the trackbar scroll events for conditional effects.
        /// </summary>
        private void ConditionalScroll(object sender, System.EventArgs e)
        {
            EffectParameters eff = new EffectParameters();

            if (1 <= axis.Length)
                rbConditionalAxis2.Enabled = true;
            else
                rbConditionalAxis2.Enabled = false;

            eff = ChangeParameter();
            UpdateConditionalGroupBox(eff);

        }


        /// <summary>
        /// Handles direction changes.
        /// </summary>
        private void DirectionChanged(object sender, System.EventArgs e)
        {
            int[] direction = new int[2];
            string[] values;

            foreach (RadioButton rb in DirectionGroupBox.Controls)
            {
                if (rb.Checked)
                {
                    values = rb.Tag.ToString().Split(',');
                    direction[0] = Convert.ToInt32(values[0]);
                    direction[1] = Convert.ToInt32(values[1]);
                    ChangeDirection(direction);
                    return;
                }
            }
        }


        /// <summary>
        /// Handles general parameter changes.
        /// </summary>
        private void GenScroll(object sender, System.EventArgs e)
        {
            EffectParameters eff = GetEffectParameters();

            if (GeneralDuration.Value == GeneralDuration.Maximum)
                eff.Duration = (int)DI.Infinite;
            else
                eff.Duration = GeneralDuration.Value * (int)DI.Seconds;

            eff.Gain = GeneralGain.Value;
            eff.SamplePeriod = GeneralPeriod.Value;

            UpdateGeneralParamsGroupBox(eff);

            // Some feedback drivers will fail when setting parameters that aren't supported by
            // an effect. DirectInput will will in turn pass back the driver error to the application.
            // Since these are hardware specific error messages that can't be handled individually, 
            // the app will ignore any failures returned to SetParameters().
            try
            {
                effectSelected.effect.SetParameters(eff, EffectParameterFlags.Duration | EffectParameterFlags.Gain | EffectParameterFlags.SamplePeriod | EffectParameterFlags.Start);
            }
//            catch (DirectXException) { }
            catch (SharpDX.SharpDXException  ex) { MessageBox.Show(ex.Message); }
        }




        private void EnvChanged(object sender, System.EventArgs e)
        {
            EffectParameters eff = new EffectParameters();

            eff = ChangeEnvelope();
            UpdateEnvParamsGroupBox(eff);
        }


        /// <summary>
        /// Initializes DirectInput.
        /// </summary>
        private bool InitializeDirectInput()
        {
            try
            {
                var Manager = new DirectInput();

                //Enumerate all joysticks that are attached to the system and have FF capabilities
                foreach (DeviceInstance instanceDevice in Manager.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.ForceFeedback | DeviceEnumerationFlags.AttachedOnly))
                {
                    applicationDevice = new Joystick(Manager, instanceDevice.InstanceGuid);

                    foreach (DeviceObjectInstance instanceObject in applicationDevice.GetObjects(DeviceObjectTypeFlags.Axis | DeviceObjectTypeFlags.ForceFeedbackActuator))  // Get info about all the FF axis on the device
                    {
                        int[] temp;

                        if ((instanceObject.Aspect & ObjectAspect.ForceFeedbackActuator) != 0)
                        {
                            if (null != axis)
                            {
                                temp = new int[axis.Length + 1];
                                axis.CopyTo(temp, 0);
                                axis = temp;
                            }
                            else
                            {
                                axis = new int[1];
                            }

                            // Store the offset of each axis.
                            // this must match the offset in the DInput DIJoyState, not the offset returned on instanceObject
                            // axis[axis.Length - 1] = instanceObject.Offset;
                            // HACK: ffb uses 0 and 4 as offsets, so force to that.
                            axis[axis.Length - 1] = (axis.Length - 1) * 4;
                            // Don't need to enumerate any more if 2 were found.
                            if (2 == axis.Length)
                                break;
                        }
                    }

                    if (null == applicationDevice)
                    {
                        MessageBox.Show("No force feedback device was detected. Sample will now exit.", "No suitable device");
                        return false;
                    }

                    if (axis.Length - 1 >= 1)
                        // Grab any device that contains at least one axis.
                        break;
                    else
                    {
                        axis = null;
                        applicationDevice.Dispose();
                        applicationDevice = null;
                    }
                }

                // Set the cooperative level of the device as an exclusive
                // foreground device, and attach it to the form's window handle.
                IntPtr handle = this.Handle;
                IntPtr handle2 = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                applicationDevice.SetCooperativeLevel(handle, CooperativeLevel.Foreground | CooperativeLevel.Exclusive);

                try
                {
                    applicationDevice.Acquire();
                }
                catch
                { }

                //Turn off autocenter
                applicationDevice.Properties.AutoCenter = false;

                //  applicationDevice.SendForceFeedbackCommand(ForceFeedbackCommand.Reset);


                //Set the format of the device to that of a joystick
                //                applicationDevice.SetDataFormat(DeviceDataFormat.Joystick);
                //Enumerate all the effects on the device

                foreach (EffectInfo ei in applicationDevice.GetEffects(EffectType.All))
                {
                    // Handles the enumeration of effects.
                    EffectDescription description = new EffectDescription();
                    EffectParameters eff;

                    if (((uint)ei.Type & 0xFFu) == (uint)EffectType.CustomForce)
                    {
                        // Can't create a custom force without info from the hardware vendor, so skip this effect.
                        continue;
                    }
#if false
                    else if (ei.Type.HasFlag(EffectType.Periodic))
                    {
                        // This is to filter out any Periodic effects. There are known
                        // issues with Periodic effects that will be addressed post-developer preview.
                        continue;
                    }
#endif
                    else if (((uint)ei.Type & 0xFFu) == (uint)EffectType.Hardware)
                    {
//                        if ((ei.StaticParameters & EffectParameterFlags.TypeSpecificParameters) != 0)
                            // Can't create a hardware force without info from the hardware vendor.
//                            continue;
                    }

                    // Fill in some generic values for the effect.
                    eff = FillEffStruct(ei.Type);

                    // Create the effect, using the passed in guid.
                    // Fill in the EffectDescription structure.
                    description.effect = new Effect(applicationDevice, ei.Guid, eff);
                    description.info = ei;
                    description.parameters = eff;

                    // Add this effect to the listbox, displaying the name of the effect.
                    lstEffects.Items.Add(description);
                }


                if (0 == lstEffects.Items.Count)
                {
                    // If this device has no downloadable effects, end the app.
                    MessageBox.Show("This device does not contain any downloadable effects, app will exit.");

                    // The app will validate all DirectInput objects in the frmMain_Load() event.
                    // When one is found missing, this will cause the app to exit.
                }

                // Make the first index of the listbox selected
                lstEffects.SelectedIndex = 0;
                return true;
            }
            catch (SharpDX.SharpDXException  e)
            {
                MessageBox.Show("Unable to initialize DirectInput, app will exit." + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Fills in generic values in an effect struct.
        /// </summary>
        private EffectParameters FillEffStruct(EffectType eif)
        {
            EffectParameters eff = new EffectParameters();

            eff.Directions = new int[axis.Length];
            eff.Axes = new int[axis.Length];

            eff.Duration = (int)DI.Infinite;
            eff.Gain = 10000;
            eff.SamplePeriod = 0;
            eff.TriggerButton = -1;
            eff.TriggerRepeatInterval = -1;
            eff.Flags = EffectFlags.ObjectOffsets | EffectFlags.Cartesian;
            eff.Axes = axis;

            switch ((EffectType)((uint)eif & 0xFFu))
            {
                case EffectType.Condition:
                    var set = new ConditionSet();
                    set.Conditions = new Condition[axis.Length];
                    eff.Parameters = set;
                    break;

                case EffectType.ConstantForce:
                    var cf = new ConstantForce();
                    cf.Magnitude = 5000;
                    eff.Parameters = cf;
                    break;

                case EffectType.RampForce:
                    var rf = new RampForce();
                    rf.Start = 0;
                    rf.End = 10000;
                    eff.Parameters = rf;
                    break;

                case EffectType.Periodic:
                    var pf = new PeriodicForce();
                    pf.Magnitude = 1250;
                    pf.Offset = 0;
                    pf.Period = 500000;  // 2 Hz
                    pf.Phase = 0;
                    eff.Parameters = pf;
                    break;

                case EffectType.Hardware:
                    break;
            }

            return eff;
        }
    }
}
