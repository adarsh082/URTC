# URTC – Unity Real-Time Collaboration Plugin

URTC is a lightweight Unity plugin that connects the Unity Editor to the rtc-server backend.
It allows you to manage projects, handle collaborators, and perform GitHub-linked operations directly from inside Unity—without switching tools or consoles.

The goal is to make project setup and basic repository management faster and more convenient for game developers.

## What URTC Does

Sign in with GitHub (OAuth handled through the backend)

Create and manage projects from within Unity

Automatically create GitHub repositories through the server

Add/remove collaborators with a few clicks

Trigger backend repo actions such as push/pull

Keep user and project metadata synced with the rtc-server

Provide a clean Inspector/EditorWindow interface for all operations

URTC focuses on improving workflows, not replacing full version control.

## How It Works

The plugin sends requests to the rtc-server REST API

The server authenticates via GitHub OAuth

Project actions (repo creation, collaborators, etc.) are forwarded to GitHub

Unity receives updated project/session info

All results appear directly inside the Unity Editor window

URTC acts as the Unity frontend for the collaboration backend.

## Editor UI

URTC includes custom Unity Editor windows written in C#.
They are built using:

EditorWindow

ScriptableObject for caching session data

UnityWebRequest for API calls

JSON utilities for parsing server responses

The layout is intentionally simple so it blends into normal Unity workflows.

## Planned Additions

These are optional future features (not in current version):

Real-time collaboration through WebSockets

In-editor presence indicators

Scene diff preview

Live asset sync

Conflict resolution UI
