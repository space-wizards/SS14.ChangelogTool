namespace SS14.ChangelogTool.Models.GraphQL;

public sealed record GrapQLSearchResponse(List<GraphQLEdge> Edges, GraphQLPageInfo PageInfo);