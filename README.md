IPPT Fitness Chatbot

A web-based chatbot built with ASP.NET Core that helps users calculate their IPPT (Individual Physical Proficiency Test) scores and determine what is needed to achieve Pass, Silver, or Gold. The chatbot supports reverse calculations, where users can input known station scores (push-ups, sit-ups, or 2.4km run) and find out the required performance for the remaining stations.

Features

IPPT Score Calculator
- Compute IPPT points for Push-Ups, Sit-Ups, and 2.4km Run based on age group and gender.

Reverse IPPT Calculation
- Determine the required number of repetitions or runtime to achieve a target (Pass/Silver/Gold).

Interactive Chat Flow
- The chatbot guides the user step by step, asking for gender, age, and known station scores.

Session Management
- Uses ASP.NET Core sessions to track user input during the chat flow.

Debug Logging
- Includes detailed [DEBUG] logs for troubleshooting and score calculations.

Project Structure

FitnessChatbot/
│
├── Controllers/
│ └── ChatController.cs # Main chatbot logic
│
├── Services/
│ ├── AnswerRepository.cs # Handles reverse IPPT logic
│ ├── IPPTScorer.cs # Contains scoring tables and methods
│
├── Models/
│ └── ChatResponse.cs # Response model for chat messages
│
├── wwwroot/ # Static files
│
├── Program.cs # Application startup
├── Startup.cs # Middleware and services
└── README.md # Project documentation

Getting Started

Prerequisites

.NET 6 SDK or later

Visual Studio 2022 / VS Code

Installation

Clone the repository:
git clone https://github.com/yourusername/FitnessChatbot.git
cd FitnessChatbot

Restore dependencies:
dotnet restore

Build the project:
dotnet build

Run the application:
dotnet run

Roadmap
- Add a web-based UI for IPPT scoring.
- Add data persistence for past IPPT results.
- Expand chatbot with fitness tips and recommendations.

