// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Abstract base class representing a physical quantity dimension.
    /// Physical quantities define what is being measured (e.g., Temperature, Pressure, FlowRate).
    /// </summary>
    /// <remarks>
    /// This is the base class for all physical quantity types. Each subclass represents
    /// a specific dimension of measurement. Units of measure must be compatible with
    /// the quantity type they are used with.
    /// </remarks>
    public abstract class PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity type.
        /// </summary>
        public abstract string QuantityName { get; }

        /// <summary>
        /// Returns a string representation of the physical quantity.
        /// </summary>
        /// <returns>The quantity name.</returns>
        public override string ToString()
        {
            return QuantityName;
        }
    }

    /// <summary>
    /// Represents the physical quantity of temperature.
    /// </summary>
    public sealed class Temperature : PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "Temperature";
    }

    /// <summary>
    /// Represents the physical quantity of pressure.
    /// </summary>
    public sealed class Pressure : PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "Pressure";
    }

    /// <summary>
    /// Represents the physical quantity of distance (length, thickness, diameter, etc.).
    /// </summary>
    public sealed class Distance : PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "Distance";
    }

    /// <summary>
    /// Abstract base class for flow rate quantities.
    /// Flow rate can be measured as mass, molar, or volumetric flow.
    /// </summary>
    public abstract class FlowRate : PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "FlowRate";
    }

    /// <summary>
    /// Represents mass flow rate (e.g., kg/s, lb/h).
    /// </summary>
    public sealed class MassFlowRate : FlowRate
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "MassFlowRate";
    }

    /// <summary>
    /// Represents molar flow rate (e.g., mol/s, kmol/h).
    /// </summary>
    public sealed class MolarFlowRate : FlowRate
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "MolarFlowRate";
    }

    /// <summary>
    /// Represents volumetric flow rate (e.g., m3/s, L/min, ft3/s).
    /// </summary>
    public sealed class VolumetricFlowRate : FlowRate
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "VolumetricFlowRate";
    }
}
