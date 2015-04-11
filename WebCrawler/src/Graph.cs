using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>Represents a directed unweighted graph structure
/// </summary>
namespace WebCrawler
{
    public class Graph<T>
    {
        /*
        // Contains the child nodes for each vertex of the graph
        // assuming that the vertices are numbered 0 ... Size-1
        private List<T>[] childNodes;

        /// <summary>Constructs an empty graph of given size</summary>
        /// <param name="size">number of vertices</param>
        public Graph(int size)
        {
            childNodes = new List<T>[size];
            for (int i = 0; i < size; i++)
            {
                // Assing an empty list of adjacents for each vertex
                childNodes[i] = new List<T>();
            }
        }

        /// <summary>Constructs a graph by given list of
        /// child nodes (successors) for each vertex</summary>
        /// <param name="childNodes">children for each node</param>
        public Graph(List<T>[] childNodes)
        {
            this.childNodes = childNodes;
        }

        /// <summary>
        /// Returns the size of the graph (number of vertices)
        /// </summary>
        public int Size
        {
            get { return childNodes.Length; }
        }

        /// <summary>Adds new edge from u to v</summary>
        /// <param name="u">the starting vertex</param>
        /// <param name="v">the ending vertex</param>
        public void AddEdge(T u, T v)
        {

            childNodes[u].Add(v);
        }

        /// <summary>Removes the edge from u to v if such exists
        /// </summary>
        /// <param name="u">the starting vertex</param>
        /// <param name="v">the ending vertex</param>
        public void RemoveEdge(int u, int v)
        {
            childNodes[u].Remove(v);
        }

        /// <summary>
        /// Checks whether there is an edge between vertex u and v
        /// </summary>
        /// <param name="u">the starting vertex</param>
        /// <param name="v">the ending vertex</param>
        /// <returns>true if there is an edge between
        /// vertex u and vertex v</returns>

        public bool HasEdge(T u, T v)
        {
            bool hasEdge = u.Contains(v);
            return hasEdge;
        }

        /// <summary>Returns the successors of a given vertex
        /// </summary>
        /// <param name="v">the vertex</param>
        /// <returns>list of all successors of vertex v</returns>
        public IList<int> GetSuccessors(int v)
        {
            return childNodes[v];
        }*/
    }
}