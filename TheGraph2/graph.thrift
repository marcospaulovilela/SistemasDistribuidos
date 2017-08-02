namespace * TheGraph.Thrift

exception VertexAlreadyExists {}
exception VertexDontExists {}

exception EdgeAlreadyExists {}
exception EdgeDontExists {}

struct vertex
{
    1: i32 name,
    2: i32 color,
    3: string description,
    4: double weight
}

struct edge
{
    1: i32 v1,
    2: i32 v2,
    3: double weight,
    4: bool directed,
    5: string description
}

struct graph{
    1: list<vertex> V,
    2: list<edge> E
}

service TheGraph{
    graph G(1: bool scan),

    bool createVertex(1: vertex V) throws (1: VertexAlreadyExists vae) ,
    bool createEdge(1: edge E) throws (1: VertexDontExists vde, 2: EdgeAlreadyExists eae),
    bool createDuplicatedEdge(1: edge E),
	
    bool deleteVertex(1: vertex V) throws (1: VertexDontExists vde),
    bool deleteEdge(1: edge E) throws (1: EdgeDontExists ede),
	bool deleteDuplicatedEdge(edge E),

    bool updateVertex(1: vertex V) throws (1: VertexDontExists vde),
    bool updateEdge(1: edge E) throws (1: EdgeDontExists ede),
    bool updateDuplicatedEdge(1: edge E),
	
    bool copyVertex(1: vertex E),
    bool copyEdge(1: edge V),
    
    vertex readV(1: i32 name) throws (1: VertexDontExists vde),
    edge readE(1: i32 V_Name1, 2: i32 V_Name2, 3: bool directed) throws (1: EdgeDontExists ede),

    list<edge> getEdges(1: vertex V),
    list<vertex> getVertex(1: edge E),
    list<vertex> getNeighborhood(1: vertex V),
	list<i32> bfs(i32 start, i32 target)
}