﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Atavism
{

    public class UGUIToolBarMenu : MonoBehaviour
    {

        [SerializeField] UGUICharacterPanel characterWindow;
        [SerializeField] UGUIInventory inventoryWindow;
        [SerializeField] UGUISkillsWindow skillsWindow;
        [SerializeField] UGUIQuestList questsWindow;
        [SerializeField] UGUIMailBox mailWindow;
        [SerializeField] UGUIGuildPanel guildWindow;
        [SerializeField] GameObject adminMenu;
        [SerializeField] GameObject arenaMenu;
        [SerializeField] GameObject arenaExitMenu;
        //  [SerializeField] Text adminKeyText;
        // [SerializeField] Text WBKeyText;
        [SerializeField] Text characterKeyText;
        [SerializeField] TextMeshProUGUI TMPCharacterKeyText;
        [SerializeField] Text inventoryKeyText;
        [SerializeField] TextMeshProUGUI TMPInventoryKeyText;
        [SerializeField] Text skillsKeyText;
        [SerializeField] TextMeshProUGUI TMPSkillsKeyText;
        [SerializeField] Text questsKeyText;
        [SerializeField] TextMeshProUGUI TMPQuestsKeyText;
        [SerializeField] Text mailboxKeyText;
        [SerializeField] TextMeshProUGUI TMPMailboxKeyText;
        [SerializeField] Text guildKeyText;
        [SerializeField] TextMeshProUGUI TMPGuildKeyText;
        [SerializeField] Text arenaKeyText;
        [SerializeField] TextMeshProUGUI TMPArenaKeyText;
        [SerializeField] Text socialKeyText;
        [SerializeField] TextMeshProUGUI TMPSocialKeyText;
        bool showing = true;
        // Use this for initialization
        void Start()
        {

            if (AtavismSettings.Instance.ArenaInstances.Contains(SceneManager.GetActiveScene().name) || AtavismSettings.Instance.DungeonInstances.Contains(SceneManager.GetActiveScene().name))
            {
                if (arenaExitMenu != null)
                    arenaExitMenu.SetActive(true);
                if (arenaMenu != null)
                    arenaMenu.SetActive(false);
            }
            else
            {
                if (arenaExitMenu != null)
                    arenaExitMenu.SetActive(false);
                if (arenaMenu != null)
                    arenaMenu.SetActive(true);
            }
            
            if (showing)
            {
                Atavism3rdPersonInput a3pi = Camera.main.transform.GetComponent<Atavism3rdPersonInput>();
                if (a3pi != null)
                {
                    if (a3pi.mouseLookLocked)
                    {
                        Hide();
                    }
                    else
                    {
                        Show();
                    }   
                }
                else
                {
                    Show();
                }
            }
            else
            {
                Hide();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if ((Input.GetKeyDown(AtavismSettings.Instance.openToolBarMenuKey)&& !AtavismSettings.Instance.useSameKeyForBarMenuFromGameSetting)||
                (Input.GetKeyDown(AtavismSettings.Instance.openGameSettingsKey)&& AtavismSettings.Instance.useSameKeyForBarMenuFromGameSetting))
            {
                Toggle();
            }
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.key) || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) )&& !ClientAPI.UIHasFocus())
                if (characterWindow != null)
                    characterWindow.Toggle();
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().inventory.key)  || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) )&& !ClientAPI.UIHasFocus())
                if (inventoryWindow != null)
                    inventoryWindow.Toggle();
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().skills.key) || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) ) && !ClientAPI.UIHasFocus())
                if (skillsWindow != null)
                    skillsWindow.Toggle();
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().quest.key)  || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) )&& !ClientAPI.UIHasFocus())
                if (questsWindow != null)
                    questsWindow.Toggle();
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().mail.key)  || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) )&& !ClientAPI.UIHasFocus())
                if (mailWindow != null)
                    mailWindow.Toggle();
            if ((Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().guild.key)  || Input.GetKeyDown(AtavismSettings.Instance.GetKeySettings().character.altKey) )&& !ClientAPI.UIHasFocus())
                if (guildWindow != null)
                    guildWindow.Toggle();
            if (inventoryKeyText != null)
                inventoryKeyText.text = AtavismSettings.Instance.GetKeySettings().inventory.key.ToString().ToUpper();
            if (TMPInventoryKeyText != null)
                TMPInventoryKeyText.text = AtavismSettings.Instance.GetKeySettings().inventory.key.ToString().ToUpper();
            if (characterKeyText != null)
                characterKeyText.text = AtavismSettings.Instance.GetKeySettings().character.key.ToString().ToUpper();
            if (TMPCharacterKeyText != null)
                TMPCharacterKeyText.text = AtavismSettings.Instance.GetKeySettings().character.key.ToString().ToUpper();
            if (mailboxKeyText != null)
                mailboxKeyText.text = AtavismSettings.Instance.GetKeySettings().mail.key.ToString().ToUpper();
            if (TMPMailboxKeyText != null)
                TMPMailboxKeyText.text = AtavismSettings.Instance.GetKeySettings().mail.key.ToString().ToUpper();
            if (guildKeyText != null)
                guildKeyText.text = AtavismSettings.Instance.GetKeySettings().guild.key.ToString().ToUpper();
            if (TMPGuildKeyText != null)
                TMPGuildKeyText.text = AtavismSettings.Instance.GetKeySettings().guild.key.ToString().ToUpper();
            if (questsKeyText != null)
                questsKeyText.text = AtavismSettings.Instance.GetKeySettings().quest.key.ToString().ToUpper();
            if (TMPQuestsKeyText != null)
                TMPQuestsKeyText.text = AtavismSettings.Instance.GetKeySettings().quest.key.ToString().ToUpper();
            if (skillsKeyText != null)
                skillsKeyText.text = AtavismSettings.Instance.GetKeySettings().skills.key.ToString().ToUpper();
            if (TMPSkillsKeyText != null)
                TMPSkillsKeyText.text = AtavismSettings.Instance.GetKeySettings().skills.key.ToString().ToUpper();
            if (arenaKeyText != null)
                arenaKeyText.text = AtavismSettings.Instance.GetKeySettings().arena.key.ToString().ToUpper();
            if (TMPArenaKeyText != null)
                TMPArenaKeyText.text = AtavismSettings.Instance.GetKeySettings().arena.key.ToString().ToUpper();
            if (socialKeyText != null)
                socialKeyText.text = AtavismSettings.Instance.GetKeySettings().social.key.ToString().ToUpper();
            if (TMPSocialKeyText != null)
                TMPSocialKeyText.text = AtavismSettings.Instance.GetKeySettings().social.key.ToString().ToUpper();


        }
        public void Toggle()
        {
            if (showing)
                Hide();
            else
                Show();
        }
        public void Show()
        {
            AtavismSettings.Instance.isMenuBarOpened = true;
            showing = true;
            foreach (Transform child in transform)
            {
                if(!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
                
            }
            int adminLevel = 0;
            try
            {
                adminLevel = (int)ClientAPI.GetObjectProperty(ClientAPI.GetPlayerOid(), "adminLevel");
            }
            catch (Exception e)
            {
            }
            if (adminLevel >= 3 && adminMenu != null)
            {
                adminMenu.SetActive(true);
            }
            else if (adminMenu != null && adminMenu.activeSelf )
                adminMenu.SetActive(false);
        }
        
        public void Hide()
        {
            AtavismSettings.Instance.isMenuBarOpened = false;
            showing = false;
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }
    }
}