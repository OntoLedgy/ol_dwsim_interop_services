// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// This file is part of the OntoLedgy Thermodynamics Architecture and is
// dual-licensed:
//
//   1. Open source under the GNU Affero General Public License v3.0 or
//      later (AGPL-3.0-or-later). See the LICENSE file in the repository
//      root for the full licence text and NOTICE for attribution.
//   2. Commercial under a separate proprietary licence offered by
//      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Engine
{
    /// <summary>
    /// Unit tests for SessionManager class.
    /// </summary>
    public class SessionManagerTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly SessionManager _sessionManager;
        private readonly FlowsheetContextConfig _defaultConfig;

        public SessionManagerTests()
        {
            _mockLogger = new Mock<ILogger>();
            _defaultConfig = CreateTestConfig();
            _sessionManager = new SessionManager(_mockLogger.Object, _defaultConfig);
        }

        public void Dispose()
        {
            _sessionManager?.Dispose();
        }

        private FlowsheetContextConfig CreateTestConfig()
        {
            return new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .Build();
        }

        #region Session Retrieval Tests

        [Fact]
        public void GetSession_WithValidId_ReturnsContext()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var createResult = _sessionManager.CreateSession();
            var getResult = _sessionManager.GetSession(createResult.Data);

            Assert.True(createResult.Success);
            Assert.True(getResult.Success);
            Assert.NotNull(getResult.Data);
        }

        [Fact]
        public void GetSession_WithInvalidId_ReturnsFailure()
        {
            var invalidId = Guid.NewGuid();
            var result = _sessionManager.GetSession(invalidId);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionExists_WithValidId_ReturnsTrue()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var createResult = _sessionManager.CreateSession();
            var exists = _sessionManager.SessionExists(createResult.Data);

            Assert.True(createResult.Success);
            Assert.True(exists);
        }

        [Fact]
        public void SessionExists_WithInvalidId_ReturnsFalse()
        {
            var exists = _sessionManager.SessionExists(Guid.NewGuid());
            Assert.False(exists);
        }

        [Fact]
        public void GetAllSessions_ReturnsAllActiveSessions()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var first = _sessionManager.CreateSession();
            var second = _sessionManager.CreateSession();

            Assert.True(first.Success);
            Assert.True(second.Success);

            var sessions = _sessionManager.GetAllSessions();
            Assert.Equal(2, sessions.Count);
            Assert.Contains(sessions, s => s.SessionId == first.Data);
            Assert.Contains(sessions, s => s.SessionId == second.Data);
        }

        [Fact]
        public void GetAllSessions_WhenEmpty_ReturnsEmptyList()
        {
            var sessions = _sessionManager.GetAllSessions();
            Assert.Empty(sessions);
        }

        #endregion

        #region Session Creation Tests

        [Fact]
        public void CreateSession_WithDefaultConfig_ReturnsUniqueGuid()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var result = _sessionManager.CreateSession();

            Assert.True(result.Success);
            Assert.NotEqual(Guid.Empty, result.Data);
        }

        [Fact]
        public void CreateSession_MultipleTimes_ReturnsUniqueGuids()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var result1 = _sessionManager.CreateSession();
            var result2 = _sessionManager.CreateSession();

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.NotEqual(result1.Data, result2.Data);
        }

        [Fact]
        public void CreateSession_WithCustomConfig_UsesProvidedConfig()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var customConfig = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("CustomFlowsheet")
                .Build();

            var result = _sessionManager.CreateSession(config: customConfig);

            Assert.True(result.Success);

            var sessions = _sessionManager.GetAllSessions();
            var sessionInfo = sessions.FirstOrDefault(s => s.SessionId == result.Data);

            Assert.NotNull(sessionInfo);
            Assert.Equal("CustomFlowsheet", sessionInfo.FlowsheetName);
        }

        [Fact]
        public void CreateSession_RecordsCreationTime()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var before = DateTime.UtcNow;
            var result = _sessionManager.CreateSession();
            var after = DateTime.UtcNow;

            Assert.True(result.Success);

            var sessions = _sessionManager.GetAllSessions();
            var sessionInfo = sessions.FirstOrDefault(s => s.SessionId == result.Data);

            Assert.NotNull(sessionInfo);
            Assert.InRange(sessionInfo.CreatedAt, before, after);
        }

        #endregion

        #region Session Disposal Tests

        [Fact]
        public void CloseSession_WithValidId_RemovesFromRegistry()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var createResult = _sessionManager.CreateSession();
            var closeResult = _sessionManager.CloseSession(createResult.Data);

            Assert.True(createResult.Success);
            Assert.True(closeResult.Success);
            Assert.False(_sessionManager.SessionExists(createResult.Data));
        }

        [Fact]
        public void CloseSession_WithInvalidId_ReturnsSuccessFalse()
        {
            var closeResult = _sessionManager.CloseSession(Guid.NewGuid());

            Assert.True(closeResult.Success);
            Assert.False(closeResult.Data);
        }

        [Fact]
        public void CloseSession_CalledTwice_IsIdempotent()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var createResult = _sessionManager.CreateSession();
            var firstClose = _sessionManager.CloseSession(createResult.Data);
            var secondClose = _sessionManager.CloseSession(createResult.Data);

            Assert.True(createResult.Success);
            Assert.True(firstClose.Success);
            Assert.True(secondClose.Success);
            Assert.False(secondClose.Data);
        }

        [Fact]
        public void CloseAllSessions_RemovesAllSessions()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            _sessionManager.CreateSession();
            _sessionManager.CreateSession();

            _sessionManager.CloseAllSessions();

            var sessions = _sessionManager.GetAllSessions();
            Assert.Empty(sessions);
        }

        [Fact]
        public void Dispose_ClosesAllActiveSessions()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            _sessionManager.CreateSession();
            _sessionManager.CreateSession();

            _sessionManager.Dispose();

            var sessions = _sessionManager.GetAllSessions();
            Assert.Empty(sessions);
        }

        [Fact]
        public void GetSession_AfterClose_ReturnsNotFound()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var createResult = _sessionManager.CreateSession();
            _sessionManager.CloseSession(createResult.Data);

            var getResult = _sessionManager.GetSession(createResult.Data);

            Assert.False(getResult.Success);
        }

        #endregion

        #region Metadata and Edge Case Tests

        [Fact]
        public void SessionInfo_ContainsCorrectMetadata()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var result = _sessionManager.CreateSession();
            var sessions = _sessionManager.GetAllSessions();
            var info = sessions.FirstOrDefault(s => s.SessionId == result.Data);

            Assert.True(result.Success);
            Assert.NotNull(info);
            Assert.Equal(result.Data, info.SessionId);
            Assert.Equal(_defaultConfig.FlowsheetName, info.FlowsheetName);
        }

        [Fact]
        public void SessionInfo_ReflectsInitializationStatus()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var result = _sessionManager.CreateSession();
            var info = _sessionManager.GetAllSessions().FirstOrDefault(s => s.SessionId == result.Data);

            Assert.True(result.Success);
            Assert.NotNull(info);
            Assert.True(info.IsInitialized);
        }

        [Fact]
        public void GetAllSessions_ReturnsCorrectMetadata()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var first = _sessionManager.CreateSession(flowsheetName: "Flowsheet-A");
            var second = _sessionManager.CreateSession(flowsheetName: "Flowsheet-B");

            Assert.True(first.Success);
            Assert.True(second.Success);

            var sessions = _sessionManager.GetAllSessions();
            var firstInfo = sessions.FirstOrDefault(s => s.SessionId == first.Data);
            var secondInfo = sessions.FirstOrDefault(s => s.SessionId == second.Data);

            Assert.NotNull(firstInfo);
            Assert.NotNull(secondInfo);
            Assert.Equal("Flowsheet-A", firstInfo.FlowsheetName);
            Assert.Equal("Flowsheet-B", secondInfo.FlowsheetName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new SessionManager(null, _defaultConfig));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void CreateSession_WithNullConfig_UsesDefault()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var result = _sessionManager.CreateSession(config: null);
            var info = _sessionManager.GetAllSessions().FirstOrDefault(s => s.SessionId == result.Data);

            Assert.True(result.Success);
            Assert.NotNull(info);
            Assert.Equal(_defaultConfig.FlowsheetName, info.FlowsheetName);
        }

        #endregion
    }
}
