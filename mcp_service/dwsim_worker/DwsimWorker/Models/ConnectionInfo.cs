// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents information about a stream connection to a unit operation.
    /// This is an immutable record-like class capturing connection details.
    /// </summary>
    public sealed class ConnectionInfo
    {
        /// <summary>
        /// Gets the ID of the connected stream.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Gets the ID of the unit operation the stream is connected to.
        /// </summary>
        public string UnitId { get; }

        /// <summary>
        /// Gets the name of the port on the unit operation (e.g., "Inlet1", "VaporOut").
        /// </summary>
        public string PortName { get; }

        /// <summary>
        /// Gets the timestamp when the connection was established.
        /// </summary>
        public DateTime ConnectedAt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionInfo"/> class.
        /// </summary>
        /// <param name="streamId">The ID of the connected stream.</param>
        /// <param name="unitId">The ID of the unit operation.</param>
        /// <param name="portName">The name of the port on the unit operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public ConnectionInfo(string streamId, string unitId, string portName)
            : this(streamId, unitId, portName, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionInfo"/> class with a specific timestamp.
        /// </summary>
        /// <param name="streamId">The ID of the connected stream.</param>
        /// <param name="unitId">The ID of the unit operation.</param>
        /// <param name="portName">The name of the port on the unit operation.</param>
        /// <param name="connectedAt">The timestamp when the connection was established.</param>
        /// <exception cref="ArgumentNullException">Thrown when any string parameter is null or whitespace.</exception>
        public ConnectionInfo(string streamId, string unitId, string portName, DateTime connectedAt)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(unitId))
                throw new ArgumentNullException(nameof(unitId), "Unit ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentNullException(nameof(portName), "Port name cannot be null or empty.");

            StreamId = streamId;
            UnitId = unitId;
            PortName = portName;
            ConnectedAt = connectedAt;
        }

        /// <summary>
        /// Returns a string representation of the connection.
        /// </summary>
        /// <returns>A string containing the connection details.</returns>
        public override string ToString()
        {
            return $"Connection: Stream '{StreamId}' -> Unit '{UnitId}' Port '{PortName}' (Connected: {ConnectedAt:yyyy-MM-dd HH:mm:ss} UTC)";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current ConnectionInfo.
        /// </summary>
        /// <param name="obj">The object to compare with the current ConnectionInfo.</param>
        /// <returns>True if the specified object is equal to the current ConnectionInfo; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ConnectionInfo other)
            {
                return StreamId == other.StreamId &&
                       UnitId == other.UnitId &&
                       PortName == other.PortName;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current ConnectionInfo.
        /// </summary>
        /// <returns>A hash code for the current ConnectionInfo.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (StreamId?.GetHashCode() ?? 0);
                hash = hash * 23 + (UnitId?.GetHashCode() ?? 0);
                hash = hash * 23 + (PortName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
