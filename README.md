# The Microservices Messaging Model (MMM)

The Microservices Messaging Model (MMM) is a suite of tools and APIs designed to simplify the understanding of communication patterns within a microservices architecture. It automates the extraction of message flows between microservices directly from the codebase.

## Key Outputs

MMM generates two key outputs to provide a comprehensive view of microservice interactions:

- **Communication Model Diagrams**: Visual representations that depict the interactions between involved microservices. Each diagram focuses on a single microservice, highlighting the messages it produces and consumes in its interactions with other services.
- **Message Details Wiki Pages**: Comprehensive wiki pages offering in-depth information about the messages produced and consumed by each microservice. This includes details like message name, content structure, and any relevant usage info.

## Benefits

By automatically generating these outputs, MMM offers several advantages:

- **Improved Visibility**: Builds a clear understanding of message flows within the microservices architecture, making it easier to identify potential bottlenecks or integration issues.
- **Simplified Communication**: The communication model diagrams provide a shared language for developers working on different microservices, facilitating collaboration and reducing misunderstandings.
- **Reduced Documentation Overhead**: The automated generation of message details wiki pages minimizes the manual effort required to document message specifications.

Overall, MMM acts as a valuable tool for promoting effective communication and streamlining development efforts within a microservices ecosystem.

## Supported Frameworks

Currently, MMM supports three approaches for detecting messages within .NET microservices:

- Using DotNetCore.CAP library ([DotNetCore.CAP](https://www.nuget.org/packages/DotNetCore.CAP))
- Using MMM.Attributes package for annotation ([MMM.Attributes](https://www.nuget.org/packages/MMM.Attributes))
