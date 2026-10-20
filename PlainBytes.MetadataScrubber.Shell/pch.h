// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <strsafe.h>
#include <new>

void DllAddRef();
void DllRelease();

// Must match Package.appxmanifest SurrogateServer / Verb Clsid
class __declspec(uuid("b7f4e2a1-3c5d-4e8f-9a2b-1d6c8e0f4a37")) ScrubExplorerCommand;

HRESULT ScrubExplorerCommand_CreateInstance(REFIID riid, void** ppv);
