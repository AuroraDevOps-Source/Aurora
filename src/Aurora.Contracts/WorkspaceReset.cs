namespace Aurora.Contracts;

public sealed record WorkspaceResetPreview(string Revision, int Manifests, int Drafts, int Orders);
public sealed record WorkspaceResetCommand(string Revision, string Confirmation);
public sealed record WorkspaceResetResult(int Manifests, int Drafts, int Orders);
