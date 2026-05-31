// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record RunCellIdentity(
    string ImplementationId,
    string ScenarioId,
    string ProtocolId,
    string ExecutionProfileId,
    string NetworkProfile,
    string LoadProfileId,
    int Connections,
    int StreamsPerConnection,
    int Repetition)
{
    public static RunCellIdentity Create(RunCell cell)
    {
        return new RunCellIdentity(
            cell.Implementation.Id,
            cell.Scenario.Id,
            ProtocolIds.Normalize(cell.Protocol),
            ExecutionProfiles.ToId(cell.ExecutionProfile),
            cell.NetworkProfile,
            string.IsNullOrWhiteSpace(cell.LoadProfileId) ? "no-load-profile" : cell.LoadProfileId,
            cell.Connections,
            cell.StreamsPerConnection,
            cell.Repetition);
    }

    public IReadOnlyList<string> PathSegments => [
        ArtifactLayout.SanitizeSegment(ImplementationId),
        ArtifactLayout.SanitizeSegment(ScenarioId),
        ArtifactLayout.SanitizeSegment(ProtocolId),
        ArtifactLayout.SanitizeSegment(ExecutionProfileId),
        ArtifactLayout.SanitizeSegment(NetworkProfile),
        ArtifactLayout.SanitizeSegment(LoadProfileId),
        $"c{Connections}-s{StreamsPerConnection}-r{Repetition}"
    ];

    public string ToSlug()
    {
        return string.Join("__", PathSegments);
    }

    public string ToKey()
    {
        return string.Join(
            "\u001f",
            ImplementationId,
            ScenarioId,
            ProtocolId,
            ExecutionProfileId,
            NetworkProfile,
            LoadProfileId,
            Connections.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StreamsPerConnection.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Repetition.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
