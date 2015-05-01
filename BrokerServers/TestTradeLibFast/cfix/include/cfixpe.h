#pragma once

/*----------------------------------------------------------------------
 * Purpose:
 *		Cfix auxilliary header file - defines structures used for
 *		PE-based test modules. Do not include directly - 
 *		include either cfixapi.h or cfix.h.
 *
 *		Note: Test code should include cfix.h.
 *
 *            cfixaux.h        cfixkrio.h
 *              ^ ^ ^--------+     ^
 *             /   \          \   /
 *            /     \          \ /
 *		cfixapi.h  cfixpe.h  cfixkr.h
 *			^	  ^	  ^         
 *			|	 /	  |         
 *			|	/	  |         
 *		  [cfix]	cfix.h      
 *                    ^         
 *                    |         
 *                    |         
 *          [Test DLLs/Drivers] 
 *
 * Copyright:
 *		2008, Johannes Passing (passing at users.sourceforge.net)
 *
 * This file is part of cfix.
 *
 * cfix is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * cfix is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with cfix.  If not, see <http://www.gnu.org/licenses/>.
 */

#include <cfixaux.h>

#ifndef MAKELONG
#define MAKELONG(a, b)      ((LONG)(((SHORT)((ULONG_PTR)(a) & 0xffff)) | ((ULONG)((SHORT)((DWORD_PTR)(b) & 0xffff))) << 16))
#endif

/*++
	Exception Description:
		Thrown when a test case turns out to be inconclusive. 
		Handled internally.
--*/
#define EXCEPTION_TESTCASE_INCONCLUSIVE ( ( ULONG ) 0x8004AFFEUL )

/*++
	Exception Description:
		Thrown when a test case has failed.
		Handled internally.
--*/
#define EXCEPTION_TESTCASE_FAILED		( ( ULONG ) 0x8004AFFFUL )

/*++
	Exception Description:
		Thrown when a test case has failed and the test case should
		be aborted.
		Handled internally.
--*/
#define EXCEPTION_TESTCASE_FAILED_ABORT	( ( ULONG ) 0x8004AFFDUL )


/*----------------------------------------------------------------------
 *
 * Definitions for test map.
 *
 * Sampe usage:
 * 
 * CFIX_BEGIN_FIXTURE(Ts01)
 * 	CFIX_FIXTURE_SETUP(Setup)
 * 	CFIX_FIXTURE_TEARDOWN(Tdown)
 * 	CFIX_FIXTURE_ENTRY(TcFunc1)
 * 	CFIX_FIXTURE_ENTRY(TcFunc2)
 * CFIX_END_FIXTURE()
 *
 */
typedef enum
{
	CfixEntryTypeEnd			= 0,
	CfixEntryTypeSetup			= 1,
	CfixEntryTypeTeardown		= 2,
	CfixEntryTypeTestcase		= 3
} CFIX_ENTRY_TYPE;


/*++
	Description:
		Prototype of setup, teardown and testcase routines.
--*/
typedef VOID ( CFIXCALLTYPE * CFIX_PE_TESTCASE_ROUTINE )();

/*++
	Struct Description:
		Defines an entry in a test map. Only used by
		CFIX_GET_FIXTURE_ROUTINE-routines.
--*/
typedef struct _CFIX_PE_DEFINITION_ENTRY
{
	CFIX_ENTRY_TYPE Type;
	PCWSTR Name;
	CFIX_PE_TESTCASE_ROUTINE Routine;
} CFIX_PE_DEFINITION_ENTRY, *PCFIX_PE_DEFINITION_ENTRY;

/*++
	Struct Description:
		Defines a test map. Only used by
		CFIX_GET_FIXTURE_ROUTINE-routines.
--*/
typedef struct _CFIX_TEST_PE_DEFINITION
{
	ULONG ApiVersion;
	PCFIX_PE_DEFINITION_ENTRY Entries;
} CFIX_TEST_PE_DEFINITION, *PCFIX_TEST_PE_DEFINITION;

/*++
	Description:
		Prototype of export of test DLL that is generated by test map.
--*/
typedef PCFIX_TEST_PE_DEFINITION ( CFIXCALLTYPE * CFIX_GET_FIXTURE_ROUTINE )();

#define CFIX_PE_API_VERSION	MAKELONG( 1, 0 )

#define CFIX_BEGIN_FIXTURE(name)									\
EXTERN_C __declspec(dllexport)										\
PCFIX_TEST_PE_DEFINITION CFIXCALLTYPE __CfixFixturePe##name()		\
{																	\
	static CFIX_PE_DEFINITION_ENTRY Entries[] = {					\

#define CFIX_FIXTURE_SETUP(func)									\
	{ CfixEntryTypeSetup, __CFIX_WIDE( #func ), func },								

#define CFIX_FIXTURE_TEARDOWN(func)									\
	{ CfixEntryTypeTeardown,__CFIX_WIDE( #func ), func },								

#define CFIX_FIXTURE_ENTRY(func)									\
	{ CfixEntryTypeTestcase, __CFIX_WIDE( #func ), func },								

#define CFIX_END_FIXTURE()											\
	{ CfixEntryTypeEnd, NULL, NULL }								\
	};																\
	static CFIX_TEST_PE_DEFINITION Fixture = {						\
		CFIX_PE_API_VERSION,										\
		Entries														\
	};																\
	return &Fixture;												\
}			


#define CfixPtrFromRva( base, rva ) ( ( ( PUCHAR ) base ) + rva )

#define CFIX_FIXTURE_EXPORT_PREFIX "__CfixFixturePe"
#define CFIX_FIXTURE_EXPORT_PREFIX_CCH 15

#if _WIN64
#define CFIX_FIXTURE_EXPORT_PREFIX_MANGLED		CFIX_FIXTURE_EXPORT_PREFIX
#define CFIX_FIXTURE_EXPORT_PREFIX_MANGLED_CCH	( CFIX_FIXTURE_EXPORT_PREFIX_CCH )
#else
#define CFIX_FIXTURE_EXPORT_PREFIX_MANGLED		"_" CFIX_FIXTURE_EXPORT_PREFIX
#define CFIX_FIXTURE_EXPORT_PREFIX_MANGLED_CCH	( CFIX_FIXTURE_EXPORT_PREFIX_CCH + 1 )
#endif