using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

class TTKSTweak : ModuleTweak
{
	public TTKSTweak(BombComponent bombComponent) : base(bombComponent, "TurnKeyAdvancedModule")
	{
		// Overrides the left key after array with correct names 
		componentType.SetValue("LeftAfterA", new string[]
		{
			"Password",
			"Crazy Talk",
			"Who's on First",
			"Keypad",
			"Listening",
			"Orientation Cube"
		});

		// TTKS left key modified interaction code
		bombComponent.GetComponent<KMSelectable>().Children[0].OnInteract = () =>
		{
			if (!component.GetValue<bool>("bActivated") || component.GetValue<bool>("bLeftKeyTurned"))
			{
				return false;
			}
			KMBombInfo mBombInfo = component.GetValue<KMBombInfo>("mBombInfo");
			IList sModules = componentType.GetValue<IList>("sModules");
			KMBombModule mModule = component.GetValue<KMBombModule>("mModule");
			List<string> solvedModuleNames = mBombInfo.GetSolvedModuleNames();
			List<string> source = mBombInfo.GetSolvableModuleNames();
			for (int i = 0; i < mBombInfo.GetSolvedModuleNames().Count; i++)
				source.Remove(mBombInfo.GetSolvedModuleNames()[i]);
			int ct = 0;
			for (int i = 0; i < sModules.Count; i++)
			{
				if (!sModules[i].GetValue<bool>("bRightKeyTurned"))
					ct++;
			}
			bool flag = ct == 0;
			ct = 0;
			for (int i = 0; i < sModules.Count; i++)
			{
				if (sModules[i].GetValue<int>("mPriority") < component.GetValue<int>("mPriority") && !sModules[i].GetValue<bool>("bLeftKeyTurned"))
					ct++;
			}
			bool flag2 = ct == 0;
			bool flag3 = source.Count((string m) => componentType.GetValue<string[]>("LeftAfterA").Contains(m)) == 0;
			bool flag4 = solvedModuleNames.Count((string m) => componentType.GetValue<string[]>("LeftBeforeA").Contains(m)) == 0;
			if (flag && flag2 && flag3 && flag4)
			{
				component.GetValue<Animator>("LeftKeyAnim").SetBool("IsUnlocked", true);
				component.GetValue<KMAudio>("mAudio").PlaySoundAtTransform("TurnTheKeyFX", bombComponent.transform);
				component.SetValue("bLeftKeyTurned", true);
				if (component.GetValue<bool>("bRightKeyTurned"))
				{
					mModule.HandlePass();
				}
				return false;
			}
			mModule.HandleStrike();
			component.GetValue<Animator>("LeftKeyAnim").SetTrigger("WrongTurn");
			component.GetValue<KMAudio>("mAudio").PlaySoundAtTransform("WrongKeyTurnFX", bombComponent.transform);
			return false;
		};

		// TTKS right key modified interaction code
		bombComponent.GetComponent<KMSelectable>().Children[1].OnInteract = () =>
		{
			if (!component.GetValue<bool>("bActivated") || component.GetValue<bool>("bRightKeyTurned"))
			{
				return false;
			}
			KMBombInfo mBombInfo = component.GetValue<KMBombInfo>("mBombInfo");
			IList sModules = componentType.GetValue<IList>("sModules");
			KMBombModule mModule = component.GetValue<KMBombModule>("mModule");
			List<string> solvedModuleNames = mBombInfo.GetSolvedModuleNames();
			List<string> source = mBombInfo.GetSolvableModuleNames();
			for (int i = 0; i < mBombInfo.GetSolvedModuleNames().Count; i++)
				source.Remove(mBombInfo.GetSolvedModuleNames()[i]);
			int ct = 0;
			for (int i = 0; i < sModules.Count; i++)
			{
				if (sModules[i].GetValue<int>("mPriority") > component.GetValue<int>("mPriority") && !sModules[i].GetValue<bool>("bRightKeyTurned"))
					ct++;
			}
			bool flag = ct == 0;
			bool flag2 = source.Count((string m) => componentType.GetValue<string[]>("RightAfterA").Contains(m)) == 0;
			bool flag3 = solvedModuleNames.Count((string m) => componentType.GetValue<string[]>("RightBeforeA").Contains(m)) == 0;
			if (flag && flag2 && flag3)
			{
				component.GetValue<Animator>("RightKeyAnim").SetBool("IsUnlocked", true);
				component.GetValue<KMAudio>("mAudio").PlaySoundAtTransform("TurnTheKeyFX", bombComponent.transform);
				component.SetValue("bRightKeyTurned", true);
				if (component.GetValue<bool>("bLeftKeyTurned"))
				{
					mModule.HandlePass();
				}
				return false;
			}
			mModule.HandleStrike();
			component.GetValue<Animator>("RightKeyAnim").SetTrigger("WrongTurn");
			component.GetValue<KMAudio>("mAudio").PlaySoundAtTransform("WrongKeyTurnFX", bombComponent.transform);
			return false;
		};

		/* The following modifications were made to the key interaction code:
		 * - Keys cannot be selected again once turned
		 * - Keys will now perform the strike animation and audio when turned incorrectly like TTK
		 * - Each key now requires ALL their before modules are solved instead of at least one of each */
	}
}